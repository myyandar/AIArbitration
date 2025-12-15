namespace AIArbitration.Core.Entities
{
    public class PerformancePrediction
    {
        // Basic predictions
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public TimeSpan EstimatedLatency { get; set; }
        public decimal ReliabilityScore { get; set; } // 0-100
        public decimal SuccessProbability { get; set; } // 0-1

        // Detailed metrics
        public Dictionary<string, object> Metrics { get; set; } = new()
        {
            ["throughput"] = 0.0,
            ["error_rate"] = 0.0,
            ["availability"] = 1.0
        };

        // Historical data
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public int HistoricalRequests { get; set; }

        // Health indicators
        public bool IsHealthy
        {
            get
            {
                return ReliabilityScore >= 70 && (double)SuccessProbability >= 0.95;
            }
        }

        public string HealthStatus => IsHealthy ? "Healthy" : "Degraded";

        // Confidence
        public decimal Confidence { get; set; } = 0.8m;

        // Predictions
        public Dictionary<string, decimal> PredictionScores { get; set; } = new();
        public decimal PredictedSuccessRate { get; set; }
        public decimal EstimatedCostPerRequest { get; set; }
        public DateTime Timestamp { get; set; }
        public TimeSpan PredictedLatency { get; set; }
        public string ModelId { get; set; }
        public string ProviderId { get; set; }
        public decimal EstimatedCostPerToken { get; set; }
        public int EstimatedTokensPerSecond { get; set; }
        public int ThroughputCapacity { get; set; }
        public decimal CurrentLoad { get; set; }
        public int HistoricalDataPoints { get; set; }
        public int RecentFailures { get; set; }
        public string Notes { get; set; }

        // Helper methods
        public bool MeetsSLA(TimeSpan maxLatency) => EstimatedLatency <= maxLatency;
        public bool IsReliable(decimal minScore = 80) => ReliabilityScore >= minScore;

        // Navigation
        public virtual AIModel Model { get; set; } = null!;
    }
}
