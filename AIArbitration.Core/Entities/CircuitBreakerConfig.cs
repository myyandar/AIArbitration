namespace AIArbitration.Core.Entities
{
    public class CircuitBreakerConfig
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Thresholds
        public int FailureThreshold { get; set; } = 5; // Number of failures before tripping
        public decimal FailurePercentageThreshold { get; set; } = 50; // Percentage of failures before tripping
        public TimeSpan FailureThresholdTimeWindow { get; set; } = TimeSpan.FromMinutes(1); // Sliding window for failures
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(1); // Time before circuit transitions from OPEN to HALF_OPEN

        // Half-open state
        public int MaxHalfOpenTestRequests { get; set; } = 1; // Number of test requests allowed in half-open state
        public int SuccessThreshold { get; set; } = 1; // Number of successful test requests required to close circuit

        // Advanced settings
        public bool EnableSlidingWindow { get; set; } = true;
        public List<string>? ExcludedExceptionTypes { get; set; } // Exceptions that shouldn't count as failures
        public bool EnableAdaptiveTimeout { get; set; } = false;
        public TimeSpan? MinimumTimeout { get; set; }
        public TimeSpan? MaximumTimeout { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<CircuitBreaker> Circuits { get; set; } = new List<CircuitBreaker>();
    }
}
