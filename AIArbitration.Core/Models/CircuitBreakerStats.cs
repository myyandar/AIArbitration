using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class CircuitBreakerStats
    {
        public string CircuitId { get; set; } = string.Empty;
        public CircuitState CurrentState { get; set; }

        // Statistics
        public int TotalRequests { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public int ConsecutiveFailures { get; set; }
        public decimal SuccessRate { get; set; }
        public decimal RecentFailureRate { get; set; }

        // Timestamps
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime? LastStateChange { get; set; }
        public TimeSpan? TimeSinceLastStateChange { get; set; }

        // Health status
        public bool IsHealthy { get; set; }

        // Configuration
        public CircuitBreakerConfig Config { get; set; } = new();
    }
}
