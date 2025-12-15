using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class CircuitBreakerEvent
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CircuitId { get; set; } = string.Empty;
        public CircuitEventType EventType { get; set; }
        public string Details { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual CircuitBreaker Circuit { get; set; } = null!;
    }
}
