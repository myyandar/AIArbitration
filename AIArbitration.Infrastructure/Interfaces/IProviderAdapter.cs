using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Data.SqlTypes;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Interfaces
{
    /// <summary>
    /// Main interface for provider adapters
    /// </summary>
    public interface IProviderAdapter
    {
        // Basic provider information
        string ProviderName { get; }
        string ProviderId { get; }
        string BaseUrl { get; }

        // Core operations
        Task<ModelResponse> SendChatCompletionAsync(ChatRequest request);
        Task<StreamingModelResponse> SendStreamingChatCompletionAsync(ChatRequest request);
        Task<EmbeddingResponse> SendEmbeddingAsync(EmbeddingRequest request);
        Task<ModerationResponse> SendModerationAsync(ModerationRequest request);

        // Model management
        Task<List<ProviderModelInfo>> GetAvailableModelsAsync();
        Task<ProviderModelInfo?> GetModelInfoAsync(string modelId);

        // Health and monitoring
        Task<ProviderHealthStatus> CheckHealthAsync();
        Task<PerformancePrediction> GetMetricsAsync();

        // Cost management
        Task<CostEstimation> EstimateCostAsync(ChatRequest request);
        Task<CostEstimation> EstimateCostAsync(string modelId, int inputTokens, int outputTokens);

        // Configuration
        ProviderConfiguration GetConfiguration();
        Task UpdateConfigurationAsync(ProviderConfiguration configuration);
    }
}
