using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CircuitHealth
    {
        public string CircuitId { get; set; } = string.Empty;
        public string CircuitName { get; set; } = string.Empty;
        public HealthStatus Status { get; set; }
        public decimal HealthScore { get; set; } // 0-100
        public List<string> Issues { get; set; } = new();
        public Dictionary<string, decimal> Metrics { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public CircuitState CurrentState { get; set; }
        public bool IsHealthy { get; set; }
        public decimal FailureRate { get; set; }
        public int RecentFailures { get; set; }
        public int RecentSuccesses { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public DateTime? LastStateChange { get; set; }
        public TimeSpan? TimeSinceLastStateChange { get; set; }
        public CircuitBreakerConfig Config { get; set; } = new();
    }
}
