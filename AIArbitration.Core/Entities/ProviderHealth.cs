namespace AIArbitration.Core.Entities
{
    public class ProviderHealth
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProviderId { get; set; } = string.Empty;

        // Health Metrics
        public ProviderHealthStatus ProviderHealthStatus { get; set; } 
        public decimal UptimePercentage { get; set; } // Last 24 hours
        public decimal SuccessRate { get; set; } // Last 100 requests
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public int ErrorRate { get; set; } // Errors per minute
        public int RateLimitRemaining { get; set; }
        public DateTime RateLimitResetAt { get; set; }

        // Incident Tracking
        public DateTime? LastIncidentAt { get; set; }
        public string? LastIncidentReason { get; set; }
        public IncidentSeverity? LastIncidentSeverity { get; set; }

        // Performance Metrics
        public int ActiveConnections { get; set; }
        public int QueuedRequests { get; set; }
        public decimal Throughput { get; set; } // Requests per second

        // Timestamps
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int HealthScore { get; set; }
        public TimeSpan ResponseTime { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastCheckedAt { get; set; }

        // Navigation
        public virtual ModelProvider Provider { get; set; } = null!;
    }

    public enum ProviderHealthStatus
    {
        Unknown = 0,
        Healthy,        // All systems normal
        Degraded,       // Performance issues
        Unstable,       // Intermittent failures
        Down,           // Complete outage
        RateLimited,    // Rate limited by provider
        Maintenance     // Planned maintenance
    }

    public enum IncidentSeverity
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Critical = 4
    }
}
