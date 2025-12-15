using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ModelResponse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RequestId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string? ProviderId { get; set; }
        public string Content { get; set; } = string.Empty;
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public decimal Cost { get; set; }
        public TimeSpan Latency { get; set; }
        public DateTime RespondedAt { get; set; } = DateTime.UtcNow;
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
        public string SystemFingerprint { get; set; } = string.Empty;

        // Compliance-related properties
        public List<string>? Flags { get; set; } // "content_filter", "bias_detected", etc.
        public Dictionary<string, object>? ComplianceMetadata { get; set; }
        public string? AuditTrailId { get; set; }

        public virtual ChatRequest Request { get; set; } = null!;
        public virtual AIModel Model { get; set; } = null!;
        public TimeSpan ProcessingTime { get; set; }
        public bool Success { get; set; }
        public string ModelUsed { get; set; }
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string? SessionId { get; set; }
        public long Created { get; set; }
        public DateTime Timestamp { get; set; }
        public int TotalTokens { get; set; }
        public List<ModelChoice> Choices { get; set; }
        public string Provider { get; set; }
    }
}
