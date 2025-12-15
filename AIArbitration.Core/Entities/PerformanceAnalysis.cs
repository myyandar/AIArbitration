using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class PerformanceAnalysis
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public DateTime AnalysisPeriodStart { get; set; }
        public DateTime AnalysisPeriodEnd { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public decimal SuccessRate => TotalRequests > 0 ? (decimal)SuccessfulRequests / TotalRequests * 100 : 0;
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P50Latency { get; set; }
        public TimeSpan P90Latency { set; get; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public decimal AverageCost { get; set; }
        public int AverageInputTokens { get; set; }
        public int AverageOutputTokens { get; set; }
        public Dictionary<string, decimal> SuccessRateByTaskType { get; set; } = new();
        public Dictionary<string, TimeSpan> LatencyByTaskType { get; set; } = new();
        public Dictionary<string, decimal> CostByTaskType { get; set; } = new();
        public TimeSpan Latency { get; set; }
        public bool Success { get; set; }
        public int FailedRequests { get; set; }
        public TimeSpan TotalLatency { get; set; }
        public TimeSpan MinLatency { get; set; }
        public TimeSpan MaxLatency { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation
        public AIModel Model { get; set; } 
        public ModelProvider Provider { get; set; }
        public List<PerformanceAnomaly> Anomalies { get; set; } = new();
        public List<PerformancePrediction> PerformancePrediction { get; set; } = new();
        public double TokensPerSecond { get; set; }
    }
}
