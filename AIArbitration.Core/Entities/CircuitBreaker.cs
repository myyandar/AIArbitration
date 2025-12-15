using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class CircuitBreaker
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string CircuitId { get; set; } = string.Empty; // e.g., "Provider_OpenAI", "Model_gpt-4"
        public CircuitState CurrentState { get; set; } = CircuitState.Closed;

        // Statistics
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public int HalfOpenTestRequests { get; set; }

        // Timestamps
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime? LastStateChange { get; set; }
        public string? LastFailureException { get; set; }
        public string? LastTripException { get; set; }

        // Configuration
        public string? ConfigId { get; set; }
        public virtual CircuitBreakerConfig? Config { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<CircuitBreakerEvent> Events { get; set; } = new List<CircuitBreakerEvent>();
        public virtual ICollection<CircuitBreakerWindow> WindowEntries { get; set; } = new List<CircuitBreakerWindow>();
        public virtual ICollection<CircuitBreakerStatistics> Statistics { get; set; } = new List<CircuitBreakerStatistics>();
    }
}
