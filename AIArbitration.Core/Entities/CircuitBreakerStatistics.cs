namespace AIArbitration.Core.Entities
{
    public class CircuitBreakerStatistics
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CircuitId { get; set; } = string.Empty;
        public DateTime Date { get; set; } // For daily aggregation

        // Daily statistics
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public decimal SuccessRate => TotalRequests > 0 ? (decimal)SuccessCount / TotalRequests * 100 : 0;

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual CircuitBreaker Circuit { get; set; } = null!;
    }
}
