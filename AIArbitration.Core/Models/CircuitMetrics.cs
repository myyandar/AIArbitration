namespace AIArbitration.Core.Models
{
    public class CircuitMetrics
    {
        public string CircuitName { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public int TimeoutRequests { get; set; }
        public int RejectedRequests { get; set; }
        public decimal SuccessRate => TotalRequests > 0 ? (decimal)SuccessfulRequests / TotalRequests * 100 : 0;
        public TimeSpan AverageResponseTime { get; set; }
        public TimeSpan P95ResponseTime { get; set; }
        public TimeSpan P99ResponseTime { get; set; }
        public int CircuitBreakerTrips { get; set; }
        public TimeSpan TotalDowntime { get; set; }
        public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
    }
}
