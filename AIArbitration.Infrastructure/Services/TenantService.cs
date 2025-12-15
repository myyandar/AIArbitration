using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public class TenantService : ITenantService
    {
        private readonly ILogger<TenantService> _logger;
        private readonly ConcurrentDictionary<string, Tenant> _tenants = new();
        private readonly ConcurrentDictionary<string, TenantConstraints> _tenantConstraints = new();
        private readonly ConcurrentDictionary<string, TenantSettings> _tenantSettings = new();
        private readonly ConcurrentDictionary<string, List<ApplicationUser>> _tenantUsers = new();
        private readonly ConcurrentDictionary<string, List<Project>> _tenantProjects = new();

        public TenantService(ILogger<TenantService> logger)
        {
            _logger = logger;
        }

        public async Task<Tenant?> GetTenantByIdAsync(string tenantId)
        {
            _tenants.TryGetValue(tenantId, out var tenant);
            return tenant;
        }

        public async Task<Tenant?> GetTenantByDomainAsync(string domain)
        {
            return _tenants.Values.FirstOrDefault(t => t.Domain == domain);
        }

        public async Task<List<Tenant>> GetAllTenantsAsync()
        {
            return _tenants.Values.ToList();
        }

        public async Task<Tenant> CreateTenantAsync(CreateTenantCommand command)
        {
            var tenant = new Tenant
            {
                Name = command.Name,
                Domain = command.Domain,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                CompanyName = command.CompanyName,
                Subdomain = command.Subdomain ?? $"{command.Name.ToLower().Replace(" ", "-")}.aiarbitration.com",
                Plan = command.Plan,
                BillingCycle = command.BillingCycle,
                AdminName = command.AdminName,
                AdminEmail = command.AdminEmail
            };

            _tenants[tenant.Id] = tenant;

            // Create default constraints and settings
            _tenantConstraints[tenant.Id] = new TenantConstraints
            {
                TenantId = tenant.Id,
                MaxUsers = 10,
                MaxProjects = 5,
                AllowedProviders = new List<string> { "openai", "anthropic" },
                BlockedModels = new List<string>()
            };

            _tenantSettings[tenant.Id] = new TenantSettings
            {
                TenantId = tenant.Id,
                CompanyName = tenant.CompanyName,
                CompanyLogoUrl = tenant.CompanyLogoUrl,
                PrimaryColor = null,
                SecondaryColor = null,
                TimeZone = null,
                DateFormat = null,
                Language = null,
                EnableAuditLogging = true,
                AuditLogRetentionDays = 30,
                EnableUsageAlerts = true,
                CustomSettings = new Dictionary<string, object>()
            };

            _logger.LogInformation("Created tenant {TenantId} with name {TenantName}", tenant.Id, tenant.Name);

            return tenant;
        }

        public async Task<Tenant> UpdateTenantAsync(UpdateTenantCommand command)
        {
            if (!_tenants.TryGetValue(command.TenantId, out var tenant))
            {
                throw new InvalidOperationException($"Tenant {command.TenantId} not found");
            }

            tenant.Name = command.Name ?? tenant.Name;
            tenant.Domain = command.Domain ?? tenant.Domain;
            tenant.UpdatedAt = DateTime.UtcNow;

            _tenants[command.TenantId] = tenant;
            _logger.LogInformation("Updated tenant {TenantId}", tenant.Id);

            return tenant;
        }

        public async Task DeleteTenantAsync(string tenantId)
        {
            _tenants.TryRemove(tenantId, out _);
            _tenantConstraints.TryRemove(tenantId, out _);
            _tenantSettings.TryRemove(tenantId, out _);
            _tenantUsers.TryRemove(tenantId, out _);
            _tenantProjects.TryRemove(tenantId, out _);

            _logger.LogInformation("Deleted tenant {TenantId}", tenantId);
        }

        public async Task<TenantConstraints> GetTenantConstraintsAsync(string tenantId)
        {
            if (!_tenantConstraints.TryGetValue(tenantId, out var constraints))
            {
                constraints = new TenantConstraints
                {
                    TenantId = tenantId
                };
            }

            return constraints;
        }

        public async Task UpdateTenantConstraintsAsync(string tenantId, TenantConstraints constraints)
        {
            constraints.TenantId = tenantId;
            _tenantConstraints[tenantId] = constraints;
            _logger.LogInformation("Updated constraints for tenant {TenantId}", tenantId);
        }

        public async Task<TenantSettings> GetTenantSettingsAsync(string tenantId)
        {
            if (!_tenantSettings.TryGetValue(tenantId, out var settings))
            {
                settings = new TenantSettings
                {
                    TenantId = tenantId
                };
            }

            return settings;
        }

        public async Task UpdateTenantSettingsAsync(string tenantId, TenantSettings settings)
        {
            settings.TenantId = tenantId;
            settings.UpdatedAt = DateTime.UtcNow;
            _tenantSettings[tenantId] = settings;
            _logger.LogInformation("Updated settings for tenant {TenantId}", tenantId);
        }

        public async Task<List<ApplicationUser>> GetTenantUsersAsync(string tenantId)
        {
            if (!_tenantUsers.TryGetValue(tenantId, out var users))
            {
                users = new List<ApplicationUser>();
            }

            return users;
        }

        public async Task<ApplicationUser> AddUserToTenantAsync(string tenantId, AddUserCommand command)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = command.IsActive
            };

            if (!_tenantUsers.ContainsKey(tenantId))
            {
                _tenantUsers[tenantId] = new List<ApplicationUser>();
            }

            _tenantUsers[tenantId].Add(user);
            _logger.LogInformation("Added user {UserId} to tenant {TenantId}", user.Id, tenantId);

            return user;
        }

        public async Task RemoveUserFromTenantAsync(string tenantId, string userId)
        {
            if (_tenantUsers.TryGetValue(tenantId, out var users))
            {
                var user = users.FirstOrDefault(u => u.Id == userId);
                if (user != null)
                {
                    users.Remove(user);
                    _logger.LogInformation("Removed user {UserId} from tenant {TenantId}", userId, tenantId);
                }
            }
        }

        public async Task<List<Project>> GetTenantProjectsAsync(string tenantId)
        {
            if (!_tenantProjects.TryGetValue(tenantId, out var projects))
            {
                projects = new List<Project>();
            }

            return projects;
        }

        public async Task<Project> CreateProjectAsync(CreateProjectCommand command)
        {
            var project = new Project
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = command.TenantId,
                Name = command.Name,
                Description = command.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            if (!_tenantProjects.ContainsKey(command.TenantId))
            {
                _tenantProjects[command.TenantId] = new List<Project>();
            }

            _tenantProjects[command.TenantId].Add(project);
            _logger.LogInformation("Created project {ProjectId} for tenant {TenantId}", project.Id, project.TenantId);

            return project;
        }

        public async Task<Project> UpdateProjectAsync(UpdateProjectCommand command)
        {
            if (!_tenantProjects.TryGetValue(command.TenantId, out var projects))
            {
                throw new InvalidOperationException($"No projects found for tenant {command.TenantId}");
            }

            var project = projects.FirstOrDefault(p => p.Id == command.ProjectId);
            if (project == null)
            {
                throw new InvalidOperationException($"Project {command.ProjectId} not found");
            }

            project.Name = command.Name ?? project.Name;
            project.Description = command.Description ?? project.Description;
            project.UpdatedAt = DateTime.UtcNow;

            _logger.LogInformation("Updated project {ProjectId}", project.Id);

            return project;
        }

        public async Task DeleteProjectAsync(string projectId)
        {
            foreach (var kvp in _tenantProjects)
            {
                var project = kvp.Value.FirstOrDefault(p => p.Id == projectId);
                if (project != null)
                {
                    kvp.Value.Remove(project);
                    _logger.LogInformation("Deleted project {ProjectId} from tenant {TenantId}", projectId, kvp.Key);
                    return;
                }
            }
        }

        public async Task<List<BudgetAllocation>> GetTenantBudgetsAsync(string tenantId)
        {
            // This would typically call the BudgetService
            return new List<BudgetAllocation>();
        }

        public async Task<BudgetAllocation> CreateBudgetAsync(CreateBudgetCommand command)
        {
            // This would typically call the BudgetService
            return new BudgetAllocation
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = command.TenantId,
                ProjectId = command.ProjectId,
                Amount = command.Amount,
                Currency = command.Currency,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public async Task<BudgetAllocation> UpdateBudgetAsync(UpdateBudgetCommand command)
        {
            // This would typically call the BudgetService
            return new BudgetAllocation
            {
                Id = command.BudgetId,
                TenantId = command.TenantId,
                ProjectId = command.ProjectId,
                Amount = command.Amount,
                Currency = command.Currency,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public async Task DeleteBudgetAsync(string budgetId)
        {
            // This would typically call the BudgetService
            _logger.LogInformation("Deleted budget {BudgetId}", budgetId);
        }

        public async Task<List<ComplianceRule>> GetTenantComplianceRulesAsync(string tenantId)
        {
            // This would typically call the ComplianceService
            return new List<ComplianceRule>();
        }

        public async Task<ComplianceRule> CreateComplianceRuleAsync(CreateComplianceRuleCommand command)
        {
            // This would typically call the ComplianceService
            return new ComplianceRule
            {
                TenantId = command.TenantId,
                Name = command.Name,
                Description = command.Description,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RuleType = command.RuleType
            };
        }

        public async Task<ComplianceRule> UpdateComplianceRuleAsync(UpdateComplianceRuleCommand command)
        {
            // This would typically call the ComplianceService
            return new ComplianceRule
            {
                Id = command.RuleId,
                TenantId = command.TenantId,
                Name = command.Name,
                Description = command.Description,
                Standard = command.Standard,
                UpdatedAt = DateTime.UtcNow,
                RuleType = command.RuleType
            };
        }

        public async Task<TenantAnalytics> GetTenantAnalyticsAsync(string tenantId, DateTime start, DateTime end)
        {
            // This would typically aggregate data from various services
            return new TenantAnalytics
            {
                TenantId = tenantId,
                PeriodStart = start,
                PeriodEnd = end,
                CreatedAt = DateTime.UtcNow,
                TotalRequests = 1000,
                TotalCost = 150.75m,
                ActiveUsers = 10,
                ActiveProjects = 5
            };
        }

        public async Task<TenantStatistics> GetTenantStatisticsAsync(string tenantId)
        {
            var users = await GetTenantUsersAsync(tenantId);
            var projects = await GetTenantProjectsAsync(tenantId);

            return new TenantStatistics
            {
                TenantId = tenantId,
                TotalUsers = users.Count,
                ActiveUsers = users.Count(u => u.IsActive),
                TotalProjects = projects.Count,
                ActiveProjects = projects.Count(p => p.IsActive),
                CreatedAt = DateTime.UtcNow
            };
        }

        public async Task<Subscription> GetTenantSubscriptionAsync(string tenantId, bool isActive, TenantPlan plan)
        {
            // Default subscription
            return new Subscription
            {
                TenantId = tenantId,
                Plan = plan,
                IsActive = isActive,
                StartDate = DateTime.UtcNow.AddMonths(-1),
                EndDate = DateTime.UtcNow.AddMonths(1)
            };
        }

        public async Task<Subscription> UpdateSubscriptionAsync(string tenantId, UpdateSubscriptionCommand command)
        {
            // Update subscription logic
            return new Subscription
            {
                TenantId = tenantId,
                Plan = command.Plan,
                IsActive = command.IsActive,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public async Task<List<Invoice>> GetTenantInvoicesAsync(string tenantId)
        {
            // This would typically call the CostTrackingService
            return new List<Invoice>();
        }

        public async Task<Invoice> GenerateInvoiceAsync(decimal tax, decimal discount, decimal serviceFee, string tenantId, DateTime periodStart, DateTime periodEnd)
        {
            // This would typically call the CostTrackingService
            return new Invoice
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = tenantId,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                CreatedAt = DateTime.UtcNow,
                TotalAmount = tax + serviceFee - discount,
                Currency = "USD"
            };
        }
    }
}
