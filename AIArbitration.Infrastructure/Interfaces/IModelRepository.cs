using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IModelRepository
    {
        // Model operations
        Task<AIModel?> GetModelByIdAsync(string modelId);
        Task<AIModel?> GetModelByProviderIdAsync(string providerId, string providerModelId);
        Task<List<AIModel>> GetActiveModelsAsync();
        Task<List<AIModel>> GetModelsByCriteriaAsync(ModelQuery query);
        Task<List<AIModel>> GetModelsByProviderAsync(string providerId);
        Task<List<AIModel>> GetModelsByTierAsync(ModelTier tier);
        Task<List<AIModel>> GetModelsByCapabilityAsync(CapabilityType capability, decimal minScore = 70);

        // Provider operations
        Task<ModelProvider?> GetProviderByIdAsync(string providerId);
        Task<List<ModelProvider>> GetActiveProvidersAsync();
        Task<ProviderHealthStatus> GetProviderHealthAsync(string providerId);
        Task<List<ProviderHealth>> GetProviderHealthMetricsAsync(string providerId, DateTime start, DateTime end);

        // Performance metrics
        Task<List<PerformanceAnalysis>> GetModelPerformanceMetricsAsync(string modelId);
        Task<List<PerformanceAnalysis>> GetModelPerformanceMetricsAsync(string modelId, DateTime start, DateTime end);
        Task<PerformanceAnalysis?> GetLatestModelPerformanceAsync(string modelId);
        Task UpdateModelPerformanceAsync(string modelId, TimeSpan latency, bool success);

        // Cost and pricing
        Task<List<PricingInfo>> GetModelPricingAsync(string modelId);
        Task<PricingInfo?> GetCurrentModelPricingAsync(string modelId);

        // Statistics and analytics
        Task<ModelStatistics> GetModelStatisticsAsync(string modelId); 
        Task<ProviderStatistics> GetProviderStatisticsAsync(string providerId);

        // Updates and sync
        Task SyncModelsFromProvidersAsync();
        Task UpdateModelAsync(AIModel model);
        Task UpdateProviderAsync(ModelProvider provider);

        // Arbitration decisions
        Task RecordArbitrationDecisionAsync(ArbitrationDecision decision);
        Task<List<ArbitrationDecision>> GetArbitrationDecisionsAsync(string tenantId, DateTime start, DateTime end);

        // Model regions
        Task<List<string>> GetModelRegionsAsync(string modelId);
            // Model operations
        Task<PaginatedResult<AIModel>> GetModelsPaginatedAsync(ModelQuery query, int page, int pageSize);
        Task<int> GetActiveModelsCountAsync();

        // Batch operations
        Task<IEnumerable<AIModel>> GetModelsByIdsAsync(IEnumerable<string> modelIds);
        Task UpdateModelsAsync(IEnumerable<AIModel> models);
    }
}
