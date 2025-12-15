using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class ApiRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SessionId { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Method { get; set; } = string.Empty;
        public string ModelUsed { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public long DurationMs { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal Cost { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        // Compliance fields
        public bool ContainsPII { get; set; }
        public string[] DataCategories { get; set; } = Array.Empty<string>();
        public string ComplianceRegion { get; set; } = string.Empty;

        // Navigation
        public virtual UserSession Session { get; set; } = null!;
    }

    public class SecurityViolation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public ViolationType Type { get; set; }
        public ViolationSeverity Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string IPAddress { get; set; } = string.Empty;
        public string UserAgent { get; set; } = string.Empty;
        public string Endpoint { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty; // JSON details
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string ResolvedBy { get; set; } = string.Empty;
    }
}