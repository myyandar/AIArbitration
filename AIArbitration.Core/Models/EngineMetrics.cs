namespace AIArbitration.Core.Models
{
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
        public decimal SelectionSuccessRate { get; internal set; }
        public TimeSpan AverageSelectionTime { get; internal set; }
    }
}
