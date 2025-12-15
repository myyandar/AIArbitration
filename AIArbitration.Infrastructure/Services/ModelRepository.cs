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

namespace AIArbitration.Infrastructure.Repositories
{
    /// <summary>
    /// Repository for managing AI models, providers, and related data
    /// </summary>
    using Microsoft.EntityFrameworkCore;
    using System.Linq.Expressions;
    using System.Text.Json;

    public class ModelRepository : IModelRepository
    {
        private readonly AIArbitrationDbContext _context;
        private readonly ILogger<ModelRepository> _logger;

        public ModelRepository(AIArbitrationDbContext context, ILogger<ModelRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        #region Model Operations

        public async Task<AIModel?> GetModelByIdAsync(string modelId)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .FirstOrDefaultAsync(m => m.Id == modelId && m.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model by ID {ModelId}", modelId);
                throw;
            }
        }

        public async Task<AIModel?> GetModelByProviderIdAsync(string providerId, string providerModelId)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .FirstOrDefaultAsync(m => m.ProviderId == providerId
                        && m.ProviderModelId == providerModelId
                        && m.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting model by provider {ProviderId} and model {ProviderModelId}",
                    providerId, providerModelId);
                throw;
            }
        }

        public async Task<List<AIModel>> GetActiveModelsAsync()
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .Where(m => m.IsActive && m.Provider != null && m.Provider.IsActive)
                    .OrderBy(m => m.Provider.Code)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active models");
                throw;
            }
        }

        public async Task<List<AIModel>> GetModelsByCriteriaAsync(ModelQuery query)
        {
            try
            {
                var models = _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .AsQueryable();

                // Apply filters
                if (!string.IsNullOrEmpty(query.ProviderId))
                    models = models.Where(m => m.ProviderId == query.ProviderId);

                if (query.Tier.HasValue)
                    models = models.Where(m => m.Tier == query.Tier.Value);

                if (query.IsActive.HasValue)
                    models = models.Where(m => m.IsActive == query.IsActive.Value);

                if (query.MinIntelligenceScore.HasValue)
                    models = models.Where(m => m.IntelligenceScore >= query.MinIntelligenceScore.Value);

                if (query.MaxCostPerMillionInputTokens.HasValue)
                    models = models.Where(m => m.CostPerMillionInputTokens <= query.MaxCostPerMillionInputTokens.Value);

                if (query.MaxCostPerMillionOutputTokens.HasValue)
                    models = models.Where(m => m.CostPerMillionOutputTokens <= query.MaxCostPerMillionOutputTokens.Value);

                if (query.MinContextWindow.HasValue)
                    models = models.Where(m => m.ContextWindow >= query.MinContextWindow.Value);

                if (query.SupportsStreaming.HasValue)
                    models = models.Where(m => m.SupportsStreaming == query.SupportsStreaming.Value);

                if (query.SupportsFunctionCalling.HasValue)
                    models = models.Where(m => m.SupportsFunctionCalling == query.SupportsFunctionCalling.Value);

                if (query.SupportsVision.HasValue)
                    models = models.Where(m => m.SupportsVision == query.SupportsVision.Value);

                if (query.SupportsAudio.HasValue)
                    models = models.Where(m => m.SupportsAudio == query.SupportsAudio.Value);

                if (query.LastUpdatedAfter.HasValue)
                    models = models.Where(m => m.LastUpdated >= query.LastUpdatedAfter.Value);

                // Apply capability filter
                if (query.Capability.HasValue)
                {
                    models = models.Where(m => m.Capabilities.Any(c =>
                        c.CapabilityType == query.Capability.Value
                        && c.Score >= 70));
                }

                return await models
                    .OrderBy(m => m.Provider.Code)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by criteria");
                throw;
            }
        }

        public async Task<List<AIModel>> GetModelsByProviderAsync(string providerId)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .Where(m => m.ProviderId == providerId && m.IsActive)
                    .OrderBy(m => m.Tier)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by provider {ProviderId}", providerId);
                throw;
            }
        }

        public async Task<List<AIModel>> GetModelsByTierAsync(ModelTier tier)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .Where(m => m.Tier == tier && m.IsActive)
                    .OrderBy(m => m.Provider.Code)
                    .ThenBy(m => m.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by tier {Tier}", tier);
                throw;
            }
        }

        public async Task<List<AIModel>> GetModelsByCapabilityAsync(CapabilityType capability, decimal minScore = 70)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .Where(m => m.IsActive
                        && m.Capabilities.Any(c =>
                            c.CapabilityType == capability
                            && c.Score >= minScore))
                    .OrderBy(m => m.Provider.Code)
                    .ThenByDescending(m => m.Capabilities
                        .FirstOrDefault(c => c.CapabilityType == capability)!.Score)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by capability {Capability} with min score {MinScore}",
                    capability, minScore);
                throw;
            }
        }

        #endregion

        #region Provider Operations

        public async Task<ModelProvider?> GetProviderByIdAsync(string providerId)
        {
            try
            {
                return await _context.ModelProviders
                    .Include(p => p.Configuration)
                    .Include(p => p.Models)
                    .Include(p => p.HealthMetrics)
                    .FirstOrDefaultAsync(p => p.Id == providerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider by ID {ProviderId}", providerId);
                throw;
            }
        }

        public async Task<List<ModelProvider>> GetActiveProvidersAsync()
        {
            try
            {
                return await _context.ModelProviders
                    .Include(p => p.Configuration)
                    .Include(p => p.Models.Where(m => m.IsActive))
                    .Where(p => p.IsActive && p.IsEnabled)
                    .OrderBy(p => p.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active providers");
                throw;
            }
        }

        public async Task<ProviderHealthStatus> GetProviderHealthAsync(string providerId)
        {
            try
            {
                var latestHealth = await _context.ProviderHealth
                    .Where(h => h.ProviderId == providerId)
                    .OrderByDescending(h => h.CheckedAt)
                    .FirstOrDefaultAsync();

                return latestHealth?.ProviderHealthStatus ?? ProviderHealthStatus.Unknown;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider health for {ProviderId}", providerId);
                throw;
            }
        }

        public async Task<List<ProviderHealth>> GetProviderHealthMetricsAsync(string providerId, DateTime start, DateTime end)
        {
            try
            {
                return await _context.ProviderHealth
                    .Where(h => h.ProviderId == providerId
                        && h.CheckedAt >= start
                        && h.CheckedAt <= end)
                    .OrderBy(h => h.CheckedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting provider health metrics for {ProviderId} from {Start} to {End}",
                    providerId, start, end);
                throw;
            }
        }

        #endregion

        #region Performance Metrics

        public async Task<List<PerformanceAnalysis>> GetModelPerformanceMetricsAsync(string modelId)
        {
            try
            {
                return await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId)
                    .OrderByDescending(p => p.AnalysisPeriodEnd)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<List<PerformanceAnalysis>> GetModelPerformanceMetricsAsync(string modelId, DateTime start, DateTime end)
        {
            try
            {
                return await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId
                        && p.AnalysisPeriodStart >= start
                        && p.AnalysisPeriodEnd <= end)
                    .OrderBy(p => p.AnalysisPeriodStart)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance metrics for model {ModelId} from {Start} to {End}",
                    modelId, start, end);
                throw;
            }
        }

        public async Task<PerformanceAnalysis?> GetLatestModelPerformanceAsync(string modelId)
        {
            try
            {
                return await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId)
                    .OrderByDescending(p => p.AnalysisPeriodEnd)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting latest performance for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task UpdateModelPerformanceAsync(string modelId, TimeSpan latency, bool success)
        {
            try
            {
                var today = DateTime.UtcNow.Date;
                var periodStart = new DateTime(today.Year, today.Month, today.Day, 0, 0, 0, DateTimeKind.Utc);
                var periodEnd = periodStart.AddDays(1);

                var analysis = await _context.PerformanceAnalysis
                    .FirstOrDefaultAsync(p => p.ModelId == modelId
                        && p.AnalysisPeriodStart == periodStart
                        && p.AnalysisPeriodEnd == periodEnd);

                if (analysis == null)
                {
                    analysis = new PerformanceAnalysis
                    {
                        Id = Guid.NewGuid().ToString(),
                        ModelId = modelId,
                        AnalysisPeriodStart = periodStart,
                        AnalysisPeriodEnd = periodEnd,
                        TotalRequests = 0,
                        SuccessfulRequests = 0,
                        TotalLatency = TimeSpan.Zero,
                        MinLatency = TimeSpan.MaxValue,
                        MaxLatency = TimeSpan.Zero,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.PerformanceAnalysis.Add(analysis);
                }

                analysis.TotalRequests++;
                if (success)
                {
                    analysis.SuccessfulRequests++;
                }

                analysis.TotalLatency += latency;
                analysis.MinLatency = latency < analysis.MinLatency ? latency : analysis.MinLatency;
                analysis.MaxLatency = latency > analysis.MaxLatency ? latency : analysis.MaxLatency;
                analysis.AverageLatency = TimeSpan.FromTicks(analysis.TotalLatency.Ticks / analysis.TotalRequests);
                analysis.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating performance for model {ModelId}", modelId);
                throw;
            }
        }

        #endregion

        #region Cost and Pricing

        public async Task<List<PricingInfo>> GetModelPricingAsync(string modelId)
        {
            try
            {
                return await _context.PricingInfos
                    .Where(p => p.ModelId == modelId)
                    .OrderByDescending(p => p.UpdatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pricing for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<PricingInfo?> GetCurrentModelPricingAsync(string modelId)
        {
            try
            {
                return await _context.PricingInfos
                    .Where(p => p.ModelId == modelId)
                    .OrderByDescending(p => p.UpdatedAt)
                    .FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting current pricing for model {ModelId}", modelId);
                throw;
            }
        }

        #endregion

        #region Statistics and Analytics

        public async Task<ModelStatistics> GetModelStatisticsAsync(string modelId)
        {
            try
            {
                var model = await _context.AIModels
                    .FirstOrDefaultAsync(m => m.Id == modelId);

                if (model == null)
                    throw new ArgumentException($"Model with ID {modelId} not found");

                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var performanceData = await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId && p.AnalysisPeriodEnd >= thirtyDaysAgo)
                    .ToListAsync();

                var statistics = new ModelStatistics
                {
                    ModelId = modelId,
                    ModelName = model.DisplayName,
                    PeriodStart = thirtyDaysAgo,
                    PeriodEnd = DateTime.UtcNow
                };

                if (performanceData.Any())
                {
                    statistics.TotalRequests = performanceData.Sum(p => p.TotalRequests);
                    statistics.SuccessfulRequests = performanceData.Sum(p => p.SuccessfulRequests);
                    statistics.FailedRequests = statistics.TotalRequests - statistics.SuccessfulRequests;

                    var totalLatency = new TimeSpan(performanceData.Sum(p => p.TotalLatency.Ticks));
                    statistics.AverageLatency = statistics.TotalRequests > 0
                        ? TimeSpan.FromTicks(totalLatency.Ticks / statistics.TotalRequests)
                        : TimeSpan.Zero;
                }

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<ProviderStatistics> GetProviderStatisticsAsync(string providerId)
        {
            try
            {
                var provider = await _context.ModelProviders
                    .FirstOrDefaultAsync(p => p.Id == providerId);

                if (provider == null)
                    throw new ArgumentException($"Provider with ID {providerId} not found");

                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

                var models = await _context.AIModels
                    .Where(m => m.ProviderId == providerId && m.IsActive)
                    .ToListAsync();

                var statistics = new ProviderStatistics
                {
                    ProviderId = providerId,
                    ProviderName = provider.DisplayName,
                    ActiveModels = models.Count,
                    PeriodStart = thirtyDaysAgo,
                    PeriodEnd = DateTime.UtcNow
                };

                // Get performance data for all provider's models
                var modelIds = models.Select(m => m.Id).ToList();
                var performanceData = await _context.PerformanceAnalysis
                    .Where(p => modelIds.Contains(p.ModelId) && p.AnalysisPeriodEnd >= thirtyDaysAgo)
                    .ToListAsync();

                if (performanceData.Any())
                {
                    statistics.TotalRequests = performanceData.Sum(p => p.TotalRequests);
                    statistics.SuccessfulRequests = performanceData.Sum(p => p.SuccessfulRequests);
                    statistics.FailedRequests = statistics.TotalRequests - statistics.SuccessfulRequests;

                    // Calculate uptime from health metrics
                    var healthMetrics = await _context.ProviderHealth
                        .Where(h => h.ProviderId == providerId && h.CheckedAt >= thirtyDaysAgo)
                        .ToListAsync();

                    if (healthMetrics.Any())
                    {
                        var healthyChecks = healthMetrics.Count(h =>
                            h.ProviderHealthStatus == ProviderHealthStatus.Healthy);
                        statistics.UptimePercentage = (decimal)healthyChecks / healthMetrics.Count * 100;
                    }
                }

                return statistics;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting statistics for provider {ProviderId}", providerId);
                throw;
            }
        }

        #endregion

        #region Updates and Sync

        public async Task SyncModelsFromProvidersAsync()
        {
            try
            {
                var activeProviders = await GetActiveProvidersAsync();

                foreach (var provider in activeProviders)
                {
                    _logger.LogInformation("Syncing models for provider {ProviderName}", provider.Name);

                    // In a real implementation, you would call the provider's API here
                    // For now, we'll just update the last sync time
                    provider.LastSyncAt = DateTime.UtcNow;
                    await UpdateProviderAsync(provider);

                    _logger.LogInformation("Completed sync for provider {ProviderName}", provider.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing models from providers");
                throw;
            }
        }

        public async Task UpdateModelAsync(AIModel model)
        {
            try
            {
                var existingModel = await _context.AIModels
                    .FirstOrDefaultAsync(m => m.Id == model.Id);

                if (existingModel == null)
                {
                    _context.AIModels.Add(model);
                }
                else
                {
                    _context.Entry(existingModel).CurrentValues.SetValues(model);
                    existingModel.LastUpdated = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model {ModelId}", model.Id);
                throw;
            }
        }

        public async Task UpdateProviderAsync(ModelProvider provider)
        {
            try
            {
                var existingProvider = await _context.ModelProviders
                    .FirstOrDefaultAsync(p => p.Id == provider.Id);

                if (existingProvider == null)
                {
                    _context.ModelProviders.Add(provider);
                }
                else
                {
                    _context.Entry(existingProvider).CurrentValues.SetValues(provider);
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider {ProviderId}", provider.Id);
                throw;
            }
        }

        #endregion

        #region Arbitration Decisions

        public async Task RecordArbitrationDecisionAsync(ArbitrationDecision decision)
        {
            try
            {
                decision.Id = Guid.NewGuid().ToString();
                decision.Timestamp = DateTime.UtcNow;

                _context.ArbitrationDecisions.Add(decision);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording arbitration decision");
                throw;
            }
        }

        public async Task<List<ArbitrationDecision>> GetArbitrationDecisionsAsync(string tenantId, DateTime start, DateTime end)
        {
            try
            {
                return await _context.ArbitrationDecisions
                    .Include(d => d.SelectedModel)
                    .Include(d => d.Tenant)
                    .Include(d => d.Project)
                    .Include(d => d.User)
                    .Where(d => d.TenantId == tenantId
                        && d.Timestamp >= start
                        && d.Timestamp <= end)
                    .OrderByDescending(d => d.Timestamp)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting arbitration decisions for tenant {TenantId}", tenantId);
                throw;
            }
        }

        #endregion

        #region Model Regions

        public async Task<List<string>> GetModelRegionsAsync(string modelId)
        {
            try
            {
                var model = await _context.AIModels
                    .Include(m => m.Provider)
                    .FirstOrDefaultAsync(m => m.Id == modelId);

                if (model == null || model.Provider == null)
                    return new List<string>();

                // Parse regions from provider's SupportedRegions JSON
                if (!string.IsNullOrEmpty(model.Provider.SupportedRegions))
                {
                    try
                    {
                        var regions = JsonSerializer.Deserialize<List<string>>(model.Provider.SupportedRegions);
                        return regions ?? new List<string>();
                    }
                    catch (JsonException)
                    {
                        _logger.LogWarning("Invalid JSON in SupportedRegions for provider {ProviderId}", model.ProviderId);
                        return new List<string>();
                    }
                }

                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting regions for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<PaginatedResult<AIModel>> GetModelsPaginatedAsync(ModelQuery query, int page, int pageSize)
        {
            try
            {
                var queryable = _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.Capabilities)
                    .AsQueryable();

                // Apply filters (same as GetModelsByCriteriaAsync)
                if (query.MinIntelligenceScore.HasValue)
                    queryable = queryable.Where(m => m.IntelligenceScore >= query.MinIntelligenceScore.Value);

                if (!string.IsNullOrEmpty(query.ProviderId))
                    queryable = queryable.Where(m => m.ProviderId == query.ProviderId);

                if (query.Tier.HasValue)
                    queryable = queryable.Where(m => m.Tier == query.Tier.Value);

                if (query.IsActive.HasValue)
                    queryable = queryable.Where(m => m.IsActive == query.IsActive.Value);

                // Count total
                var totalCount = await queryable.CountAsync();

                // Apply pagination
                var items = await queryable
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();

                return new PaginatedResult<AIModel>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated models");
                throw;
            }
        }

        public async Task<int> GetActiveModelsCountAsync()
        {
            try
            {
                return await _context.AIModels
                    .CountAsync(m => m.IsActive);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active models count");
                throw;
            }
        }

        public async Task<IEnumerable<AIModel>> GetModelsByIdsAsync(IEnumerable<string> modelIds)
        {
            try
            {
                return await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.Capabilities)
                    .Where(m => modelIds.Contains(m.Id))
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting models by IDs");
                throw;
            }
        }

        public async Task UpdateModelsAsync(IEnumerable<AIModel> models)
        {
            try
            {
                var modelList = models.ToList();
                var modelIds = modelList.Select(m => m.Id).ToList();

                var existingModels = await _context.AIModels
                    .Where(m => modelIds.Contains(m.Id))
                    .ToListAsync();

                foreach (var model in modelList)
                {
                    var existing = existingModels.FirstOrDefault(m => m.Id == model.Id);
                    if (existing != null)
                    {
                        _context.Entry(existing).CurrentValues.SetValues(model);
                        existing.LastUpdated = DateTime.UtcNow;
                    }
                    else
                    {
                        await _context.AIModels.AddAsync(model);
                    }
                }

                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating multiple models");
                throw;
            }
        }
        #endregion
    }
    #region Supporting Classes

    public interface IExternalProviderService
    {
        Task<List<ExternalModel>> GetModelsAsync(string providerId);
    }

    #endregion
}

namespace AIArbitration.Core.Models
{
    public class ModelRepositoryOptions
    {
        public int DefaultQueryLimit { get; set; } = 100;
        public int MaxQueryLimit { get; set; } = 1000;
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationMinutes { get; set; } = 30;
        public bool EnableBackgroundSync { get; set; } = true;
        public int SyncIntervalMinutes { get; set; } = 60;
    }

    public class ExternalModel
    {
        public string ProviderModelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public decimal IntelligenceScore { get; set; }
        public decimal CostPerMillionInputTokens { get; set; }
        public decimal CostPerMillionOutputTokens { get; set; }
        public int ContextWindow { get; set; }
        public int MaxOutputTokens { get; set; }
        public ModelTier Tier { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsDataResidency { get; set; }
        public bool SupportsEncryptionAtRest { get; set; }
    }
}