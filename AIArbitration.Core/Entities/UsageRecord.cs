using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class UsageRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string? ApiKeyId { get; set; }
        public string? SessionId { get; set; }

        // Model information
        public string ModelId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string ModelTier { get; set; } = string.Empty;

        // Usage metrics
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens => InputTokens + OutputTokens;
        public decimal Cost { get; set; }
        public string Currency { get; set; } = "USD";
        public TimeSpan ProcessingTime { get; set; }

        // Request context
        public string RequestId { get; set; } = string.Empty;
        public string? Endpoint { get; set; }
        public string? Operation { get; set; }
        public int StatusCode { get; set; }
        public bool Success { get; set; }

        // Metadata
        public Dictionary<string, string> Metadata { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public decimal TotalRequests { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalInputTokens { get; set; }
        public int TotalOutputTokens { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public decimal EstimatedCost { get; set; }
        public string OperationType { get; set; }
        public string RecordType { get; set; }
    }
}
