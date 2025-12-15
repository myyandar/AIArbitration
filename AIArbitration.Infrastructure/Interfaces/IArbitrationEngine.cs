using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Services;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IArbitrationEngine
    {
        // Model selection
        Task<ArbitrationResult> SelectModelAsync(ArbitrationContext context);
        //Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context);

        // Execution
        Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context);
        Task<StreamingModelResponse> ExecuteStreamingAsync(ChatRequest request, ArbitrationContext context);

        // Analysis and prediction
        Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context);
        Task<PerformancePrediction> PredictPerformanceAsync(ArbitrationContext context);

        // Batch operations
        Task<List<ArbitrationResult>> SelectModelsAsync(List<ArbitrationContext> contexts);
        Task<BatchExecutionResult> ExecuteBatchAsync(List<ChatRequest> requests, ArbitrationContext context);

        // Configuration and optimization
        Task OptimizeRulesAsync(ArbitrationContext context);
        Task<ArbitrationConfiguration> GetConfigurationAsync();

        // Health and monitoring
        Task<EngineHealthStatus> GetHealthStatusAsync();
        Task<EngineMetrics> GetMetricsAsync();
    }

    /*
     
     
     */

    // Supporting types for the interface
}



    /**********

    public interface IArbitrationEngine
    {
        // Model selection
        Task<ArbitrationResult> SelectModelAsync(ArbitrationContext context);
        Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context);

        // Execution
        Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context);
        Task<StreamingModelResponse> ExecuteStreamingAsync(ChatRequest request, ArbitrationContext context);

        // Analysis and prediction
        Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context);
        Task<PerformancePrediction> PredictPerformanceAsync(ArbitrationContext context);

        // Batch operations
        Task<List<ArbitrationResult>> SelectModelsAsync(List<ArbitrationContext> contexts);
        Task<BatchExecutionResult> ExecuteBatchAsync(List<ChatRequest> requests, ArbitrationContext context);

        // Configuration and optimization
        Task OptimizeRulesAsync(ArbitrationContext context);
        Task<ArbitrationConfiguration> GetConfigurationAsync();

        // Health and monitoring
        Task<EngineHealthStatus> GetHealthStatusAsync();
        Task<EngineMetrics> GetMetricsAsync();
    }

    // Supporting types for the interface
    public class BatchExecutionResult
    {
        public List<ModelResponse> SuccessfulResponses { get; set; } = new();
        public List<FailedRequest> FailedRequests { get; set; } = new();
        public decimal TotalCost { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public Dictionary<string, int> ModelsUsed { get; set; } = new();

        // Statistics
        public int TotalRequests => SuccessfulResponses.Count + FailedRequests.Count;
        public decimal SuccessRate => TotalRequests > 0 ?
            (decimal)SuccessfulResponses.Count / TotalRequests : 0;

        public decimal AverageCostPerRequest => SuccessfulResponses.Any() ?
            TotalCost / SuccessfulResponses.Count : 0;
    }

    public class FailedRequest
    {
        public ChatRequest Request { get; set; } = null!;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string? ModelAttempted { get; set; }
        public string? ProviderAttempted { get; set; }
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? Context { get; set; }
    }

    public class ArbitrationConfiguration
    {
        public List<ArbitrationRule> Rules { get; set; } = new();
        public List<ModelProvider> AvailableProviders { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0";
    }

    public class EngineHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "Unknown";
        public Dictionary<string, bool> ComponentHealth { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }

    public class EngineMetrics
    {
        public int TotalRequestsProcessed { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public decimal TotalCost { get; set; }
        public TimeSpan AverageProcessingTime { get; set; }
        public Dictionary<string, int> ModelUsageCount { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public DateTime MetricsSince { get; set; } = DateTime.UtcNow.AddDays(-30);
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
    */
