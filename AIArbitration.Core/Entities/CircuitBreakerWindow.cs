namespace AIArbitration.Core.Entities
{
    public class CircuitBreakerWindow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CircuitId { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual CircuitBreaker Circuit { get; set; } = null!;
    }
}
