namespace AIArbitration.Core.Models
{
    public class CircuitBreakerOptions
    {
        public int DefaultFailureThreshold { get; set; } = 5;
        public decimal DefaultFailurePercentageThreshold { get; set; } = 50;
        public TimeSpan DefaultFailureThresholdTimeWindow { get; set; } = TimeSpan.FromMinutes(1);
        public TimeSpan DefaultResetTimeout { get; set; } = TimeSpan.FromMinutes(1);
        public int DefaultMaxHalfOpenTestRequests { get; set; } = 1;
        public int DefaultSuccessThreshold { get; set; } = 1;
        public bool EnableCircuitBreaker { get; set; } = true;
        public int CircuitCleanupDays { get; set; } = 30;
    }

}
