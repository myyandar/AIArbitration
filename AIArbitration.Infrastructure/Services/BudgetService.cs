using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AIArbitration.Infrastructure.Services
{
    /// <summary>
    /// Service for managing budgets, tracking usage, and sending notifications
    /// </summary>
    public class BudgetService : IBudgetService
    {
        private readonly AIArbitrationDbContext _dbContext;
        private readonly IEmailService _emailService;
        private readonly ILogger<BudgetService> _logger;
        private readonly BudgetServiceOptions _options;

        public BudgetService(
            AIArbitrationDbContext dbContext,
            IEmailService emailService,
            ILogger<BudgetService> logger,
            IOptions<BudgetServiceOptions> options)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        }

        #region Budget Management Methods

        public async Task<BudgetAllocation?> GetBudgetByIdAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                return await _dbContext.BudgetAllocations
                    .Include(b => b.Tenant)
                    .Include(b => b.Project)
                    .Include(b => b.User)
                    .Include(b => b.Notifications)
                    .FirstOrDefaultAsync(b => b.Id == budgetId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget by ID: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<List<BudgetAllocation>> GetBudgetsAsync(string tenantId, string? projectId = null, string? userId = null)
        {
            ValidateTenantId(tenantId);

            try
            {
                var query = _dbContext.BudgetAllocations
                    .Include(b => b.Tenant)
                    .Include(b => b.Project)
                    .Include(b => b.User)
                    .Where(b => b.TenantId == tenantId);

                if (!string.IsNullOrEmpty(projectId))
                {
                    query = query.Where(b => b.ProjectId == projectId);
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(b => b.UserId == userId);
                }

                return await query
                    .OrderByDescending(b => b.StartDate)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budgets for Tenant: {TenantId}, Project: {ProjectId}, User: {UserId}",
                    tenantId, projectId, userId);
                throw;
            }
        }

        public async Task<BudgetAllocation> CreateBudgetAsync(BudgetAllocation budget)
        {
            if (budget == null)
                throw new ArgumentNullException(nameof(budget));

            ValidateBudgetAllocation(budget);

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    // Set default values
                    budget.Id = Guid.NewGuid().ToString();
                    budget.UsedAmount = 0;
                    budget.LastUpdated = DateTime.UtcNow;

                    // Validate period dates
                    if (budget.StartDate >= budget.EndDate)
                        throw new ArgumentException("Start date must be before end date");

                    // Check for overlapping budgets
                    var existingBudgets = await GetBudgetsAsync(budget.TenantId, budget.ProjectId, budget.UserId);
                    var overlappingBudget = existingBudgets.FirstOrDefault(b =>
                        (b.StartDate <= budget.EndDate && b.EndDate >= budget.StartDate) &&
                        b.Period == budget.Period);

                    if (overlappingBudget != null)
                    {
                        throw new InvalidOperationException($"A budget already exists for this period: {overlappingBudget.Id}");
                    }

                    // Verify tenant exists
                    var tenantExists = await _dbContext.Tenants.AnyAsync(t => t.Id == budget.TenantId);
                    if (!tenantExists)
                        throw new InvalidOperationException($"Tenant not found: {budget.TenantId}");

                    // Verify project exists if specified
                    if (!string.IsNullOrEmpty(budget.ProjectId))
                    {
                        var projectExists = await _dbContext.Projects.AnyAsync(p => p.Id == budget.ProjectId);
                        if (!projectExists)
                            throw new InvalidOperationException($"Project not found: {budget.ProjectId}");
                    }

                    // Verify user exists if specified
                    if (!string.IsNullOrEmpty(budget.UserId))
                    {
                        var userExists = await _dbContext.Users.AnyAsync(u => u.Id == budget.UserId);
                        if (!userExists)
                            throw new InvalidOperationException($"User not found: {budget.UserId}");
                    }

                    await _dbContext.BudgetAllocations.AddAsync(budget);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget created: {BudgetId} for Tenant: {TenantId}, Amount: {Amount}",
                        budget.Id, budget.TenantId, budget.Amount);

                    return budget;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating budget for Tenant: {TenantId}", budget.TenantId);
                throw;
            }
        }

        public async Task<BudgetAllocation> UpdateBudgetAsync(BudgetAllocation budget)
        {
            if (budget == null)
                throw new ArgumentNullException(nameof(budget));

            ValidateBudgetAllocation(budget);

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var existingBudget = await _dbContext.BudgetAllocations
                        .FirstOrDefaultAsync(b => b.Id == budget.Id);

                    if (existingBudget == null)
                        throw new KeyNotFoundException($"Budget not found: {budget.Id}");

                    // Update allowed fields
                    existingBudget.Amount = budget.Amount;
                    existingBudget.Period = budget.Period;
                    existingBudget.StartDate = budget.StartDate;
                    existingBudget.EndDate = budget.EndDate;
                    existingBudget.WarningThreshold = budget.WarningThreshold;
                    existingBudget.CriticalThreshold = budget.CriticalThreshold;
                    existingBudget.SendNotifications = budget.SendNotifications;
                    existingBudget.LastUpdated = DateTime.UtcNow;

                    _dbContext.BudgetAllocations.Update(existingBudget);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget updated: {BudgetId}", budget.Id);

                    return existingBudget;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating budget: {BudgetId}", budget.Id);
                throw;
            }
        }

        public async Task DeleteBudgetAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var budget = await _dbContext.BudgetAllocations.FindAsync(budgetId);
                    if (budget == null)
                        throw new KeyNotFoundException($"Budget not found: {budgetId}");

                    // Delete associated notifications first
                    var notifications = await _dbContext.BudgetNotifications
                        .Where(n => n.BudgetId == budgetId)
                        .ToListAsync();

                    if (notifications.Any())
                    {
                        _dbContext.BudgetNotifications.RemoveRange(notifications);
                    }

                    _dbContext.BudgetAllocations.Remove(budget);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget deleted: {BudgetId}", budgetId);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting budget: {BudgetId}", budgetId);
                throw;
            }
        }

        private void ValidateBudgetAllocation(BudgetAllocation budget)
        {
            if (string.IsNullOrEmpty(budget.TenantId))
                throw new ArgumentException("Tenant ID cannot be null or empty", nameof(budget.TenantId));

            if (budget.Amount <= 0)
                throw new ArgumentException("Budget amount must be greater than zero", nameof(budget.Amount));

            if (budget.WarningThreshold < 0 || budget.WarningThreshold > 1)
                throw new ArgumentException("Warning threshold must be between 0 and 1", nameof(budget.WarningThreshold));

            if (budget.CriticalThreshold < 0 || budget.CriticalThreshold > 1)
                throw new ArgumentException("Critical threshold must be between 0 and 1", nameof(budget.CriticalThreshold));

            if (budget.WarningThreshold >= budget.CriticalThreshold)
                throw new ArgumentException("Warning threshold must be less than critical threshold");
        }

        #endregion

        #region Budget Status and Checking Methods

        public async Task<BudgetStatus> GetBudgetStatusAsync(string tenantId, string? projectId = null, string? userId = null)
        {
            ValidateTenantId(tenantId);

            try
            {
                var budget = await GetBudgetForTenantProjectUserAsync(tenantId, projectId, userId);
                return await GetBudgetStatusFromAllocationAsync(budget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget status for Tenant: {TenantId}, Project: {ProjectId}, User: {UserId}",
                    tenantId, projectId, userId);
                throw;
            }
        }

        public async Task<BudgetStatus> GetBudgetStatusAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                var budget = await _dbContext.BudgetAllocations
                    .FirstOrDefaultAsync(b => b.Id == budgetId);

                if (budget == null)
                    throw new KeyNotFoundException($"Budget not found: {budgetId}");

                return await GetBudgetStatusFromAllocationAsync(budget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget status for Budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<bool> CheckBudgetAsync(string tenantId, decimal estimatedCost, string? projectId = null, string? userId = null)
        {
            ValidateTenantId(tenantId);

            if (estimatedCost < 0)
                throw new ArgumentException("Estimated cost cannot be negative", nameof(estimatedCost));

            try
            {
                var budgetStatus = await GetBudgetStatusAsync(tenantId, projectId, userId);
                return budgetStatus.CanMakeRequest(estimatedCost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget for Tenant: {TenantId}, Estimated Cost: {EstimatedCost}",
                    tenantId, estimatedCost);

                // Default to allowing the request if we can't check
                return true;
            }
        }

        public async Task<BudgetCheckResult> CheckBudgetWithDetailsAsync(string tenantId, decimal estimatedCost, string? projectId = null, string? userId = null)
        {
            ValidateTenantId(tenantId);

            if (estimatedCost < 0)
                throw new ArgumentException("Estimated cost cannot be negative", nameof(estimatedCost));

            try
            {
                var budgetStatus = await GetBudgetStatusAsync(tenantId, projectId, userId);

                return new BudgetCheckResult
                {
                    IsAllowed = budgetStatus.CanMakeRequest(estimatedCost),
                    BudgetStatus = budgetStatus,
                    EstimatedCost = estimatedCost,
                    AvailableAmount = budgetStatus.RemainingAmount,
                    CanProceed = budgetStatus.CanMakeRequest(estimatedCost),
                    Reason = budgetStatus.CanMakeRequest(estimatedCost) ?
                        "Budget check passed" :
                        budgetStatus.IsOverBudget ? "Over budget" : "Insufficient remaining budget",
                    HealthStatus = budgetStatus.BudgetHealthStatus,
                    Timestamp = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking budget with details for Tenant: {TenantId}", tenantId);

                // Return a fallback result
                return new BudgetCheckResult
                {
                    IsAllowed = true,
                    EstimatedCost = estimatedCost,
                    AvailableAmount = decimal.MaxValue,
                    CanProceed = true,
                    Reason = "Budget check failed, defaulting to allowed",
                    HealthStatus = BudgetHealthStatus.Unknown,
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        private async Task<BudgetStatus> GetBudgetStatusFromAllocationAsync(BudgetAllocation budget)
        {
            if (budget == null)
            {
                return CreateDefaultBudgetStatus(budget?.TenantId ?? string.Empty, budget?.ProjectId, budget?.UserId);
            }

            // Get usage records for the current period
            var usageRecords = await _dbContext.UsageRecords
                .Where(r => r.TenantId == budget.TenantId &&
                           r.Timestamp >= budget.StartDate &&
                           r.Timestamp <= budget.EndDate)
                .ToListAsync();

            // Filter by project and user if specified
            if (!string.IsNullOrEmpty(budget.ProjectId))
            {
                usageRecords = usageRecords.Where(r => r.ProjectId == budget.ProjectId).ToList();
            }

            if (!string.IsNullOrEmpty(budget.UserId))
            {
                usageRecords = usageRecords.Where(r => r.UserId == budget.UserId).ToList();
            }

            var usedAmount = usageRecords.Sum(r => r.Cost);

            var budgetStatus = new BudgetStatus
            {
                TenantId = budget.TenantId,
                ProjectId = budget.ProjectId,
                UserId = budget.UserId,
                BudgetId = budget.Id,
                BudgetAmount = budget.Amount,
                Currency = budget.Currency,
                Period = budget.Period,
                PeriodStart = budget.StartDate,
                PeriodEnd = budget.EndDate,
                UsedAmount = usedAmount,
                WarningThreshold = budget.WarningThreshold * 100,
                CriticalThreshold = budget.CriticalThreshold * 100,
                RequestCount = usageRecords.Count,
                AverageRequestCost = usageRecords.Any() ? usageRecords.Average(r => r.Cost) : 0
            };

            // Calculate cost by model and provider
            budgetStatus.CostByModel = usageRecords
                .GroupBy(r => r.ModelId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Cost));

            budgetStatus.CostByProvider = usageRecords
                .Where(r => !string.IsNullOrEmpty(r.Provider))
                .GroupBy(r => r.Provider!)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Cost));

            // Calculate cost per token
            var totalTokens = usageRecords.Sum(r => r.InputTokens + r.OutputTokens);
            budgetStatus.CostPerToken = totalTokens > 0 ? usedAmount / totalTokens : 0;

            // Check notification status
            await UpdateNotificationStatus(budgetStatus, budget);

            budgetStatus.LastUpdated = DateTime.UtcNow;

            return budgetStatus;
        }

        private BudgetStatus CreateDefaultBudgetStatus(string tenantId, string? projectId, string? userId)
        {
            return new BudgetStatus
            {
                TenantId = tenantId,
                ProjectId = projectId,
                UserId = userId,
                BudgetId = "NO_BUDGET",
                BudgetAmount = decimal.MaxValue,
                Currency = "USD",
                Period = BudgetPeriod.Monthly,
                PeriodStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1),
                PeriodEnd = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1).AddMonths(1),
                UsedAmount = 0,
                WarningThreshold = 80,
                CriticalThreshold = 95,
                LastUpdated = DateTime.UtcNow,
                CostByModel = new Dictionary<string, decimal>(),
                CostByProvider = new Dictionary<string, decimal>()
            };
        }

        private async Task UpdateNotificationStatus(BudgetStatus budgetStatus, BudgetAllocation budget)
        {
            if (!budget.SendNotifications)
                return;

            var recentNotifications = await _dbContext.BudgetNotifications
                .Where(n => n.BudgetId == budget.Id &&
                           n.SentAt >= DateTime.UtcNow.AddDays(-1))
                .ToListAsync();

            budgetStatus.WarningNotificationSent = recentNotifications
                .Any(n => n.Type == BudgetNotificationType.Warning && n.SentAt > DateTime.UtcNow.AddHours(-12));

            budgetStatus.CriticalNotificationSent = recentNotifications
                .Any(n => n.Type == BudgetNotificationType.Critical && n.SentAt > DateTime.UtcNow.AddHours(-12));

            budgetStatus.OverBudgetNotificationSent = recentNotifications
                .Any(n => n.Type == BudgetNotificationType.OverBudget && n.SentAt > DateTime.UtcNow.AddHours(-12));

            budgetStatus.LastWarningNotificationAt = recentNotifications
                .Where(n => n.Type == BudgetNotificationType.Warning)
                .OrderByDescending(n => n.SentAt)
                .FirstOrDefault()?.SentAt;

            budgetStatus.LastCriticalNotificationAt = recentNotifications
                .Where(n => n.Type == BudgetNotificationType.Critical)
                .OrderByDescending(n => n.SentAt)
                .FirstOrDefault()?.SentAt;

            budgetStatus.LastOverBudgetNotificationAt = recentNotifications
                .Where(n => n.Type == BudgetNotificationType.OverBudget)
                .OrderByDescending(n => n.SentAt)
                .FirstOrDefault()?.SentAt;
        }

        private async Task<BudgetAllocation?> GetBudgetForTenantProjectUserAsync(string tenantId, string? projectId, string? userId)
        {
            var query = _dbContext.BudgetAllocations
                .Where(b => b.TenantId == tenantId);

            if (!string.IsNullOrEmpty(projectId))
            {
                query = query.Where(b => b.ProjectId == projectId);
            }

            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(b => b.UserId == userId);
            }

            var budgets = await query.ToListAsync();

            // Find the active budget (current date within start/end dates)
            var now = DateTime.UtcNow;
            var activeBudget = budgets.FirstOrDefault(b => b.StartDate <= now && b.EndDate >= now);

            // If no active budget, find the most recent budget
            return activeBudget ?? budgets
                .Where(b => b.EndDate < now)
                .OrderByDescending(b => b.EndDate)
                .FirstOrDefault();
        }

        #endregion

        #region Usage Tracking Methods

        public async Task RecordUsageAsync(UsageRecord usage)
        {
            if (usage == null)
                throw new ArgumentNullException(nameof(usage));

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    // Validate usage record
                    if (string.IsNullOrEmpty(usage.TenantId) || usage.Cost <= 0)
                    {
                        _logger.LogWarning("Invalid usage record: {UsageId}", usage.Id);
                        return;
                    }

                    // Save the usage record
                    await _dbContext.UsageRecords.AddAsync(usage);

                    // Find applicable budgets
                    var query = _dbContext.BudgetAllocations
                        .Where(b => b.TenantId == usage.TenantId &&
                                   b.StartDate <= usage.Timestamp &&
                                   b.EndDate >= usage.Timestamp);

                    if (!string.IsNullOrEmpty(usage.ProjectId))
                    {
                        query = query.Where(b => b.ProjectId == usage.ProjectId || b.ProjectId == null);
                    }

                    if (!string.IsNullOrEmpty(usage.UserId))
                    {
                        query = query.Where(b => b.UserId == usage.UserId || b.UserId == null);
                    }

                    var applicableBudgets = await query.ToListAsync();

                    foreach (var budget in applicableBudgets)
                    {
                        budget.UsedAmount += usage.Cost;
                        budget.LastUpdated = DateTime.UtcNow;
                        _dbContext.BudgetAllocations.Update(budget);

                        // Check thresholds and send notifications if needed
                        await CheckAndSendNotificationsAsync(budget);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogDebug("Usage recorded and budgets updated: {UsageId}", usage.Id);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording usage in budget service: {UsageId}", usage.Id);
                throw;
            }
        }

        public async Task RecordUsageAsync(string tenantId, string? projectId, string? userId, decimal amount, string currency = "USD")
        {
            ValidateTenantId(tenantId);

            if (amount <= 0)
                throw new ArgumentException("Amount must be greater than zero", nameof(amount));

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var query = _dbContext.BudgetAllocations
                        .Where(b => b.TenantId == tenantId &&
                                   b.StartDate <= DateTime.UtcNow &&
                                   b.EndDate >= DateTime.UtcNow);

                    if (!string.IsNullOrEmpty(projectId))
                    {
                        query = query.Where(b => b.ProjectId == projectId || b.ProjectId == null);
                    }

                    if (!string.IsNullOrEmpty(userId))
                    {
                        query = query.Where(b => b.UserId == userId || b.UserId == null);
                    }

                    var applicableBudgets = await query.ToListAsync();

                    foreach (var budget in applicableBudgets)
                    {
                        // Convert currency if needed
                        var convertedAmount = await ConvertCurrencyAsync(amount, currency, budget.Currency);

                        budget.UsedAmount += convertedAmount;
                        budget.LastUpdated = DateTime.UtcNow;
                        _dbContext.BudgetAllocations.Update(budget);

                        // Check thresholds and send notifications if needed
                        await CheckAndSendNotificationsAsync(budget);
                    }

                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogDebug("Direct usage recorded for Tenant: {TenantId}, Amount: {Amount} {Currency}",
                        tenantId, amount, currency);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording direct usage for Tenant: {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<decimal> GetCurrentUsageAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                var budget = await _dbContext.BudgetAllocations.FindAsync(budgetId);
                if (budget == null)
                    throw new KeyNotFoundException($"Budget not found: {budgetId}");

                return budget.UsedAmount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current usage for Budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<decimal> GetCurrentUsageAsync(string tenantId, string? projectId = null, string? userId = null)
        {
            ValidateTenantId(tenantId);

            try
            {
                var query = _dbContext.BudgetAllocations
                    .Where(b => b.TenantId == tenantId &&
                               b.StartDate <= DateTime.UtcNow &&
                               b.EndDate >= DateTime.UtcNow);

                if (!string.IsNullOrEmpty(projectId))
                {
                    query = query.Where(b => b.ProjectId == projectId);
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    query = query.Where(b => b.UserId == userId);
                }

                var activeBudgets = await query.ToListAsync();

                if (!activeBudgets.Any())
                    return 0;

                return activeBudgets.Sum(b => b.UsedAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current usage for Tenant: {TenantId}", tenantId);
                throw;
            }
        }

        private async Task<decimal> ConvertCurrencyAsync(decimal amount, string fromCurrency, string toCurrency)
        {
            if (fromCurrency == toCurrency)
                return amount;

            // This is a simplified currency conversion
            // In production, you would use a real currency conversion service
            var conversionRates = new Dictionary<string, decimal>
            {
                { "USD", 1.0m },
                { "EUR", 0.85m },
                { "GBP", 0.73m },
                { "JPY", 110.0m }
            };

            if (!conversionRates.ContainsKey(fromCurrency) || !conversionRates.ContainsKey(toCurrency))
            {
                _logger.LogWarning("Currency conversion not supported: {FromCurrency} to {ToCurrency}", fromCurrency, toCurrency);
                return amount; // Fallback to original amount
            }

            var amountInUsd = amount / conversionRates[fromCurrency];
            return amountInUsd * conversionRates[toCurrency];
        }

        private async Task CheckAndSendNotificationsAsync(BudgetAllocation budget)
        {
            if (!budget.SendNotifications)
                return;

            var usagePercentage = budget.Amount > 0 ? budget.UsedAmount / budget.Amount : 0;
            var recentNotifications = await _dbContext.BudgetNotifications
                .Where(n => n.BudgetId == budget.Id &&
                           n.SentAt >= DateTime.UtcNow.AddHours(-_options.NotificationCooldownHours))
                .ToListAsync();

            // Check warning threshold
            if (usagePercentage >= budget.WarningThreshold &&
                usagePercentage < budget.CriticalThreshold &&
                !recentNotifications.Any(n => n.Type == BudgetNotificationType.Warning))
            {
                await SendNotificationAsync(budget, BudgetNotificationType.Warning, usagePercentage);
            }

            // Check critical threshold
            if (usagePercentage >= budget.CriticalThreshold &&
                usagePercentage < 1.0m &&
                !recentNotifications.Any(n => n.Type == BudgetNotificationType.Critical))
            {
                await SendNotificationAsync(budget, BudgetNotificationType.Critical, usagePercentage);
            }

            // Check over budget
            if (budget.UsedAmount > budget.Amount &&
                !recentNotifications.Any(n => n.Type == BudgetNotificationType.OverBudget))
            {
                await SendNotificationAsync(budget, BudgetNotificationType.OverBudget, usagePercentage);
            }
        }

        private async Task SendNotificationAsync(BudgetAllocation budget, BudgetNotificationType type, decimal usagePercentage)
        {
            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var notification = new BudgetNotification
                    {
                        Id = Guid.NewGuid().ToString(),
                        BudgetId = budget.Id,
                        Type = type,
                        RecipientEmail = await GetRecipientEmailAsync(budget),
                        Subject = GetNotificationSubject(type, budget),
                        Message = GetNotificationMessage(type, budget, usagePercentage),
                        SentAt = DateTime.UtcNow,
                        IsRead = false
                    };

                    await _dbContext.BudgetNotifications.AddAsync(notification);
                    await _dbContext.SaveChangesAsync();

                    // Send email notification
                    if (_options.SendEmailNotifications)
                    {
                        await _emailService.SendEmailAsync(
                            notification.RecipientEmail,
                            notification.Subject,
                            notification.Message);
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget notification sent: {NotificationId} for Budget: {BudgetId}, Item: {Item}",
                        notification.Id, budget.Id, type);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending budget notification for Budget: {BudgetId}, Item: {Item}",
                    budget.Id, type);
            }
        }

        private async Task<string> GetRecipientEmailAsync(BudgetAllocation budget)
        {
            // Try to get email from tenant
            var tenant = await _dbContext.Tenants
                .Include(t => t.ContactInfo)
                .FirstOrDefaultAsync(t => t.Id == budget.TenantId);

            if (tenant?.ContactInfo != null && !string.IsNullOrEmpty(tenant.ContactInfo.Email))
                return tenant.ContactInfo.Email;

            // Fallback to default
            return _options.DefaultNotificationEmail;
        }

        private string GetNotificationSubject(BudgetNotificationType type, BudgetAllocation budget)
        {
            return type switch
            {
                BudgetNotificationType.Warning => $"Budget Warning: {budget.Amount} {budget.Currency} budget reaching limit",
                BudgetNotificationType.Critical => $"Budget Critical: {budget.Amount} {budget.Currency} budget almost exhausted",
                BudgetNotificationType.OverBudget => $"Budget Exceeded: {budget.Amount} {budget.Currency} budget exceeded",
                BudgetNotificationType.Reset => $"Budget Reset: {budget.Amount} {budget.Currency} budget reset",
                _ => $"Budget Notification: {budget.Amount} {budget.Currency}"
            };
        }

        private string GetNotificationMessage(BudgetNotificationType type, BudgetAllocation budget, decimal usagePercentage)
        {
            var usageInfo = $"{budget.UsedAmount:F2} of {budget.Amount:F2} {budget.Currency} ({usagePercentage:P2})";

            return type switch
            {
                BudgetNotificationType.Warning =>
                    $"Your budget is reaching its warning threshold. Current usage: {usageInfo}. " +
                    $"Budget period: {budget.StartDate:d} to {budget.EndDate:d}.",

                BudgetNotificationType.Critical =>
                    $"Your budget is reaching its critical threshold. Current usage: {usageInfo}. " +
                    $"Budget period: {budget.StartDate:d} to {budget.EndDate:d}. " +
                    "Consider reducing usage or increasing budget.",

                BudgetNotificationType.OverBudget =>
                    $"Your budget has been exceeded. Current usage: {usageInfo}. " +
                    $"Budget period: {budget.StartDate:d} to {budget.EndDate:d}. " +
                    "Usage may be restricted until next period.",

                BudgetNotificationType.Reset =>
                    $"Your budget has been reset. New period: {budget.StartDate:d} to {budget.EndDate:d}. " +
                    $"Amount: {budget.Amount:F2} {budget.Currency}.",

                _ => $"Budget notification. Current usage: {usageInfo}. Period: {budget.StartDate:d} to {budget.EndDate:d}."
            };
        }

        #endregion

        #region Notification Methods

        public async Task<List<BudgetNotification>> GetBudgetNotificationsAsync(string budgetId, DateTime start, DateTime end)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            if (start > end)
                throw new ArgumentException("Start date cannot be after end date", nameof(start));

            try
            {
                return await _dbContext.BudgetNotifications
                    .Where(n => n.BudgetId == budgetId &&
                               n.SentAt >= start &&
                               n.SentAt <= end)
                    .OrderByDescending(n => n.SentAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting notifications for Budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task SendBudgetNotificationsAsync()
        {
            try
            {
                var now = DateTime.UtcNow;

                // Get all active budgets
                var budgets = await _dbContext.BudgetAllocations
                    .Where(b => b.StartDate <= now && b.EndDate >= now)
                    .ToListAsync();

                _logger.LogInformation("Sending budget notifications for {Count} active budgets", budgets.Count);

                foreach (var budget in budgets)
                {
                    await CheckAndSendNotificationsAsync(budget);
                }

                _logger.LogInformation("Budget notifications sent successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending budget notifications");
                throw;
            }
        }

        public async Task MarkNotificationAsReadAsync(string notificationId)
        {
            if (string.IsNullOrEmpty(notificationId))
                throw new ArgumentException("Notification ID cannot be null or empty", nameof(notificationId));

            try
            {
                var notification = await _dbContext.BudgetNotifications.FindAsync(notificationId);
                if (notification == null)
                    throw new KeyNotFoundException($"Notification not found: {notificationId}");

                notification.IsRead = true;
                _dbContext.BudgetNotifications.Update(notification);
                await _dbContext.SaveChangesAsync();

                _logger.LogDebug("Notification marked as read: {NotificationId}", notificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification as read: {NotificationId}", notificationId);
                throw;
            }
        }

        #endregion

        #region Forecasting Methods

        public async Task<BudgetForecast> GetBudgetForecastAsync(string budgetId, int forecastDays = 30)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            if (forecastDays <= 0 || forecastDays > _options.MaxForecastDays)
                forecastDays = _options.MaxForecastDays;

            try
            {
                var budget = await _dbContext.BudgetAllocations
                    .FirstOrDefaultAsync(b => b.Id == budgetId);

                if (budget == null)
                    throw new KeyNotFoundException($"Budget not found: {budgetId}");

                return await CalculateBudgetForecastAsync(budget, forecastDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget forecast for Budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<BudgetForecast> GetBudgetForecastAsync(string tenantId, string? projectId = null, string? userId = null, int forecastDays = 30)
        {
            ValidateTenantId(tenantId);

            if (forecastDays <= 0 || forecastDays > _options.MaxForecastDays)
                forecastDays = _options.MaxForecastDays;

            try
            {
                var budget = await GetBudgetForTenantProjectUserAsync(tenantId, projectId, userId);
                if (budget == null)
                {
                    return new BudgetForecast
                    {
                        BudgetId = "NO_BUDGET",
                        ForecastDate = DateTime.UtcNow,
                        ForecastDays = forecastDays,
                        CurrentUsage = 0,
                        ForecastedUsage = 0,
                        BudgetAmount = decimal.MaxValue,
                        IsForecastedToExceed = false,
                        Confidence = 0.5m
                    };
                }

                return await CalculateBudgetForecastAsync(budget, forecastDays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget forecast for Tenant: {TenantId}", tenantId);
                throw;
            }
        }

        private async Task<BudgetForecast> CalculateBudgetForecastAsync(BudgetAllocation budget, int forecastDays)
        {
            var now = DateTime.UtcNow;
            var daysRemaining = (budget.EndDate - now).TotalDays;
            var daysElapsed = (now - budget.StartDate).TotalDays;

            if (daysElapsed <= 0)
                daysElapsed = 1;

            // Get historical usage for trend analysis
            var usageRecords = await _dbContext.UsageRecords
                .Where(r => r.TenantId == budget.TenantId &&
                           r.Timestamp >= budget.StartDate.AddDays(-30) &&
                           r.Timestamp <= now)
                .ToListAsync();

            // Filter by project and user
            if (!string.IsNullOrEmpty(budget.ProjectId))
                usageRecords = usageRecords.Where(r => r.ProjectId == budget.ProjectId).ToList();

            if (!string.IsNullOrEmpty(budget.UserId))
                usageRecords = usageRecords.Where(r => r.UserId == budget.UserId).ToList();

            var currentUsage = budget.UsedAmount;

            // Calculate daily average
            var totalDays = Math.Max(1, (now - budget.StartDate).TotalDays);
            var dailyAverage = currentUsage / (decimal)totalDays;

            // Simple linear forecast
            var forecastedUsage = currentUsage + (dailyAverage * forecastDays);

            // Calculate confidence based on data quality
            var confidence = CalculateForecastConfidence(usageRecords, (int)daysElapsed);

            return new BudgetForecast
            {
                BudgetId = budget.Id,
                ForecastDate = now,
                ForecastDays = forecastDays,
                CurrentUsage = currentUsage,
                ForecastedUsage = forecastedUsage,
                BudgetAmount = budget.Amount,
                IsForecastedToExceed = forecastedUsage > budget.Amount,
                DaysRemaining = (int)Math.Ceiling(daysRemaining),
                DailyAverageUsage = dailyAverage,
                Confidence = confidence,
                HistoricalDataPoints = usageRecords.Count,
                Notes = $"Forecast based on {daysElapsed:F1} days of data"
            };
        }

        private decimal CalculateForecastConfidence(List<UsageRecord> usageRecords, int daysElapsed)
        {
            if (usageRecords.Count < 10 || daysElapsed < 3)
                return 0.3m; // Low confidence with little data

            // Calculate variability in daily usage
            var dailyGroups = usageRecords
                .GroupBy(r => r.Timestamp.Date)
                .Select(g => g.Sum(r => r.Cost))
                .ToList();

            if (dailyGroups.Count < 3)
                return 0.5m;

            var average = dailyGroups.Average();
            var variance = dailyGroups.Average(d => Math.Pow((double)(d - average), 2));
            var stdDev = (decimal)Math.Sqrt(variance);

            // Higher variance = lower confidence
            var variabilityFactor = stdDev > 0 ? Math.Min(1.0m, average / stdDev) : 1.0m;

            // More data points = higher confidence
            var dataFactor = Math.Min(1.0m, dailyGroups.Count / 30.0m);

            return Math.Round((variabilityFactor * 0.6m + dataFactor * 0.4m) * 0.9m + 0.1m, 2);
        }

        #endregion

        #region Analysis Methods

        public async Task<BudgetAnalysis> AnalyzeBudgetUsageAsync(string budgetId, DateTime start, DateTime end)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            if (start > end)
                throw new ArgumentException("Start date cannot be after end date", nameof(start));

            try
            {
                var budget = await _dbContext.BudgetAllocations
                    .FirstOrDefaultAsync(b => b.Id == budgetId);

                if (budget == null)
                    throw new KeyNotFoundException($"Budget not found: {budgetId}");

                // Get usage records for the analysis period
                var usageRecords = await _dbContext.UsageRecords
                    .Where(r => r.TenantId == budget.TenantId &&
                               r.Timestamp >= start &&
                               r.Timestamp <= end)
                    .ToListAsync();

                // Filter by project and user
                if (!string.IsNullOrEmpty(budget.ProjectId))
                    usageRecords = usageRecords.Where(r => r.ProjectId == budget.ProjectId).ToList();

                if (!string.IsNullOrEmpty(budget.UserId))
                    usageRecords = usageRecords.Where(r => r.UserId == budget.UserId).ToList();

                var totalUsage = usageRecords.Sum(r => r.Cost);

                return new BudgetAnalysis
                {
                    BudgetId = budgetId,
                    AnalysisPeriodStart = start,
                    AnalysisPeriodEnd = end,
                    TotalUsage = totalUsage,
                    AverageDailyUsage = totalUsage / ((decimal)Math.Max(1, (end - start).TotalDays)),
                    UsageByModel = usageRecords
                        .GroupBy(r => r.ModelId)
                        .ToDictionary(g => g.Key, g => g.Sum(r => r.Cost)),
                    UsageByProvider = usageRecords
                        .Where(r => !string.IsNullOrEmpty(r.Provider))
                        .GroupBy(r => r.Provider!)
                        .ToDictionary(g => g.Key, g => g.Sum(r => r.Cost)),
                    PeakUsageDay = usageRecords
                        .GroupBy(r => r.Timestamp.Date)
                        .Select(g => new { Date = g.Key, Usage = g.Sum(r => r.Cost) })
                        .OrderByDescending(x => x.Usage)
                        .FirstOrDefault()?.Date,
                    AnalysisDate = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing budget usage for Budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<List<BudgetAlert>> GetBudgetAlertsAsync(string tenantId, DateTime start, DateTime end)
        {
            ValidateTenantId(tenantId);

            if (start > end)
                throw new ArgumentException("Start date cannot be after end date", nameof(start));

            try
            {
                var alerts = new List<BudgetAlert>();
                var budgets = await _dbContext.BudgetAllocations
                    .Where(b => b.TenantId == tenantId)
                    .ToListAsync();

                foreach (var budget in budgets)
                {
                    // Get notifications for the period
                    var notifications = await _dbContext.BudgetNotifications
                        .Where(n => n.BudgetId == budget.Id &&
                                   n.SentAt >= start &&
                                   n.SentAt <= end)
                        .ToListAsync();

                    foreach (var notification in notifications)
                    {
                        alerts.Add(new BudgetAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            BudgetId = budget.Id,
                            AlertType = notification.Type,
                            Message = notification.Message,
                            Severity = GetAlertSeverity(notification.Type),
                            TriggeredAt = notification.SentAt,
                            IsResolved = false,
                            BudgetName = $"{budget.Amount} {budget.Currency} ({budget.Period})"
                        });
                    }

                    // Check for budget exhaustion
                    if (budget.UsedAmount >= budget.Amount && budget.EndDate >= DateTime.UtcNow)
                    {
                        alerts.Add(new BudgetAlert
                        {
                            Id = Guid.NewGuid().ToString(),
                            BudgetId = budget.Id,
                            AlertType = BudgetNotificationType.OverBudget,
                            Message = $"Budget exhausted: {budget.UsedAmount:F2} of {budget.Amount:F2} {budget.Currency} used",
                            Severity = AlertSeverity.Critical,
                            TriggeredAt = DateTime.UtcNow,
                            IsResolved = false,
                            BudgetName = $"{budget.Amount} {budget.Currency} ({budget.Period})"
                        });
                    }
                }

                return alerts.OrderByDescending(a => a.TriggeredAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting budget alerts for Tenant: {TenantId}", tenantId);
                throw;
            }
        }

        private AlertSeverity GetAlertSeverity(BudgetNotificationType type)
        {
            return type switch
            {
                BudgetNotificationType.Warning => AlertSeverity.Warning,
                BudgetNotificationType.Critical => AlertSeverity.High,
                BudgetNotificationType.OverBudget => AlertSeverity.Critical,
                _ => AlertSeverity.Info
            };
        }

        #endregion

        #region Reset and Rollover Methods

        public async Task ResetBudgetAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var budget = await _dbContext.BudgetAllocations.FindAsync(budgetId);
                    if (budget == null)
                        throw new KeyNotFoundException($"Budget not found: {budgetId}");

                    budget.UsedAmount = 0;
                    budget.LastUpdated = DateTime.UtcNow;

                    _dbContext.BudgetAllocations.Update(budget);
                    await _dbContext.SaveChangesAsync();

                    // Send reset notification
                    if (budget.SendNotifications)
                    {
                        await SendNotificationAsync(budget, BudgetNotificationType.Reset, 0);
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget reset: {BudgetId}", budgetId);
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting budget: {BudgetId}", budgetId);
                throw;
            }
        }

        public async Task<bool> CanRolloverBudgetAsync(string budgetId)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            try
            {
                var budget = await _dbContext.BudgetAllocations.FindAsync(budgetId);
                if (budget == null)
                    return false;

                // Check if budget period has ended
                if (budget.EndDate > DateTime.UtcNow)
                    return false;

                // Check if there's remaining amount to rollover
                if (budget.RemainingAmount <= 0)
                    return false;

                // Check rollover settings
                return _options.AllowBudgetRollover;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if budget can rollover: {BudgetId}", budgetId);
                return false;
            }
        }

        public async Task<BudgetAllocation> RolloverBudgetAsync(string budgetId, decimal? newAmount = null)
        {
            if (string.IsNullOrEmpty(budgetId))
                throw new ArgumentException("Budget ID cannot be null or empty", nameof(budgetId));

            if (!_options.AllowBudgetRollover)
                throw new InvalidOperationException("Budget rollover is not enabled");

            try
            {
                using var transaction = await _dbContext.Database.BeginTransactionAsync();

                try
                {
                    var sourceBudget = await _dbContext.BudgetAllocations.FindAsync(budgetId);
                    if (sourceBudget == null)
                        throw new KeyNotFoundException($"Budget not found: {budgetId}");

                    // Calculate rollover amount
                    var rolloverAmount = sourceBudget.RemainingAmount;
                    var newBudgetAmount = newAmount ?? (sourceBudget.Amount + rolloverAmount);

                    // Create new budget for next period
                    var nextPeriod = CalculateNextPeriod(sourceBudget.StartDate, sourceBudget.EndDate, sourceBudget.Period);

                    var newBudget = new BudgetAllocation
                    {
                        Id = Guid.NewGuid().ToString(),
                        TenantId = sourceBudget.TenantId,
                        ProjectId = sourceBudget.ProjectId,
                        UserId = sourceBudget.UserId,
                        Period = sourceBudget.Period,
                        Amount = newBudgetAmount,
                        Currency = sourceBudget.Currency,
                        StartDate = nextPeriod.StartDate,
                        EndDate = nextPeriod.EndDate,
                        WarningThreshold = sourceBudget.WarningThreshold,
                        CriticalThreshold = sourceBudget.CriticalThreshold,
                        SendNotifications = sourceBudget.SendNotifications,
                        UsedAmount = 0,
                        LastUpdated = DateTime.UtcNow
                    };

                    await _dbContext.BudgetAllocations.AddAsync(newBudget);
                    await _dbContext.SaveChangesAsync();
                    await transaction.CommitAsync();

                    _logger.LogInformation("Budget rolled over: {SourceBudgetId} -> {NewBudgetId}, Rollover Amount: {RolloverAmount}",
                        sourceBudget.Id, newBudget.Id, rolloverAmount);

                    return newBudget;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rolling over budget: {BudgetId}", budgetId);
                throw;
            }
        }

        private (DateTime StartDate, DateTime EndDate) CalculateNextPeriod(DateTime startDate, DateTime endDate, BudgetPeriod period)
        {
            var duration = endDate - startDate;

            return period switch
            {
                BudgetPeriod.Monthly => (
                    startDate.AddMonths(1),
                    endDate.AddMonths(1)
                ),
                BudgetPeriod.Weekly => (
                    startDate.AddDays(7),
                    endDate.AddDays(7)
                ),
                BudgetPeriod.Daily => (
                    startDate.AddDays(1),
                    endDate.AddDays(1)
                ),
                BudgetPeriod.Quarterly => (
                    startDate.AddMonths(3),
                    endDate.AddMonths(3)
                ),
                BudgetPeriod.Annually => (
                    startDate.AddYears(1),
                    endDate.AddYears(1)
                ),
                _ => (
                    startDate.Add(duration),
                    endDate.Add(duration)
                )
            };
        }

        #endregion

        #region Helper Methods

        private void ValidateTenantId(string tenantId)
        {
            if (string.IsNullOrEmpty(tenantId))
                throw new ArgumentException("Tenant ID cannot be null or empty", nameof(tenantId));
        }

        Task<BudgetAllocation?> IBudgetService.GetBudgetByIdAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<List<BudgetAllocation>> IBudgetService.GetBudgetsAsync(string tenantId, string? projectId, string? userId)
        {
            throw new NotImplementedException();
        }

        Task<BudgetAllocation> IBudgetService.CreateBudgetAsync(BudgetAllocation budget)
        {
            throw new NotImplementedException();
        }

        Task<BudgetAllocation> IBudgetService.UpdateBudgetAsync(BudgetAllocation budget)
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.DeleteBudgetAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<BudgetStatus> IBudgetService.GetBudgetStatusAsync(string tenantId, string? projectId, string? userId)
        {
            throw new NotImplementedException();
        }

        Task<BudgetStatus> IBudgetService.GetBudgetStatusAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<bool> IBudgetService.CheckBudgetAsync(string tenantId, decimal estimatedCost, string? projectId, string? userId)
        {
            throw new NotImplementedException();
        }

        Task<Core.Models.BudgetCheckResult> IBudgetService.CheckBudgetWithDetailsAsync(string tenantId, decimal estimatedCost, string? projectId, string? userId)
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.RecordUsageAsync(UsageRecord usage)
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.RecordUsageAsync(string tenantId, string? projectId, string? userId, decimal amount, string currency)
        {
            throw new NotImplementedException();
        }

        Task<decimal> IBudgetService.GetCurrentUsageAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<decimal> IBudgetService.GetCurrentUsageAsync(string tenantId, string? projectId, string? userId)
        {
            throw new NotImplementedException();
        }

        Task<List<BudgetNotification>> IBudgetService.GetBudgetNotificationsAsync(string budgetId, DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.SendBudgetNotificationsAsync()
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.MarkNotificationAsReadAsync(string notificationId)
        {
            throw new NotImplementedException();
        }

        Task<Core.Models.BudgetForecast> IBudgetService.GetBudgetForecastAsync(string budgetId, int forecastDays)
        {
            throw new NotImplementedException();
        }

        Task<Core.Models.BudgetForecast> IBudgetService.GetBudgetForecastAsync(string tenantId, string? projectId, string? userId, int forecastDays)
        {
            throw new NotImplementedException();
        }

        Task<Core.Models.BudgetAnalysis> IBudgetService.AnalyzeBudgetUsageAsync(string budgetId, DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        Task<List<Core.Models.BudgetAlert>> IBudgetService.GetBudgetAlertsAsync(string tenantId, DateTime start, DateTime end)
        {
            throw new NotImplementedException();
        }

        Task IBudgetService.ResetBudgetAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<bool> IBudgetService.CanRolloverBudgetAsync(string budgetId)
        {
            throw new NotImplementedException();
        }

        Task<BudgetAllocation> IBudgetService.RolloverBudgetAsync(string budgetId, decimal? newAmount)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    #region Supporting Classes

    public class BudgetCheckResult
    {
        public bool IsAllowed { get; set; }
        public BudgetStatus? BudgetStatus { get; set; }
        public decimal EstimatedCost { get; set; }
        public decimal AvailableAmount { get; set; }
        public bool CanProceed { get; set; }
        public string Reason { get; set; } = string.Empty;
        public BudgetHealthStatus HealthStatus { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class BudgetForecast
    {
        public string BudgetId { get; set; } = string.Empty;
        public DateTime ForecastDate { get; set; }
        public int ForecastDays { get; set; }
        public decimal CurrentUsage { get; set; }
        public decimal ForecastedUsage { get; set; }
        public decimal BudgetAmount { get; set; }
        public bool IsForecastedToExceed { get; set; }
        public int DaysRemaining { get; set; }
        public decimal DailyAverageUsage { get; set; }
        public decimal Confidence { get; set; } // 0-1 scale
        public int HistoricalDataPoints { get; set; }
        public string Notes { get; set; } = string.Empty;
    }

    public class BudgetAnalysis
    {
        public string BudgetId { get; set; } = string.Empty;
        public DateTime AnalysisPeriodStart { get; set; }
        public DateTime AnalysisPeriodEnd { get; set; }
        public decimal TotalUsage { get; set; }
        public decimal AverageDailyUsage { get; set; }
        public Dictionary<string, decimal> UsageByModel { get; set; } = new();
        public Dictionary<string, decimal> UsageByProvider { get; set; } = new();
        public DateTime? PeakUsageDay { get; set; }
        public DateTime AnalysisDate { get; set; }
    }

    public class BudgetAlert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BudgetId { get; set; } = string.Empty;
        public BudgetNotificationType AlertType { get; set; }
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public DateTime TriggeredAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string BudgetName { get; set; } = string.Empty;
    }

    public enum AlertSeverity
    {
        Info = 0,
        Warning,
        High,
        Critical
    }

    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
    }

    public class BudgetServiceOptions
    {
        public bool SendEmailNotifications { get; set; } = true;
        public string DefaultNotificationEmail { get; set; } = "admin@example.com";
        public int NotificationCooldownHours { get; set; } = 12;
        public int MaxForecastDays { get; set; } = 90;
        public bool AllowBudgetRollover { get; set; } = true;
        public decimal MaxRolloverPercentage { get; set; } = 0.5m; // 50% max rollover
    }

    #endregion
}