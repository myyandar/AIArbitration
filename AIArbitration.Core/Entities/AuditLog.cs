using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class AuditLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Basic Information
        public string EventType { get; set; } = string.Empty; // e.g., "user_login", "model_used", "api_key_created"
        public string EventCategory { get; set; } = string.Empty; // "authentication", "data_access", "configuration", "system"
        public string EventSubcategory { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        // Actor Information
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public string? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? ActorType { get; set; } // "user", "system", "api_key", "service_account"
        public string? ActorId { get; set; }
        public string? ActorName { get; set; }

        // Resource Information
        public string? ResourceType { get; set; } // "user", "model", "api_key", "budget", "project"
        public string? ResourceId { get; set; }
        public string? ResourceName { get; set; }
        public string? ResourceAction { get; set; } // "created", "updated", "deleted", "accessed"

        // Technical Details
        public string? SessionId { get; set; }
        public string? RequestId { get; set; }
        public string? CorrelationId { get; set; }
        public string? TraceId { get; set; }
        public string? SpanId { get; set; }

        // Network & Location
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }

        // HTTP Request Details
        public string? HttpMethod { get; set; }
        public string? Endpoint { get; set; }
        public string? Url { get; set; }
        public int? StatusCode { get; set; }
        public long? DurationMs { get; set; }

        // Business Context
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Environment { get; set; } // "production", "staging", "development"
        public string? ServiceName { get; set; } = "arbitration-engine";

        // Data Changes (Before/After for GDPR Right to Rectification)
        public string? OldValues { get; set; } // JSON serialized
        public string? NewValues { get; set; } // JSON serialized
        public string? ChangedFields { get; set; } // JSON array of field names

        // Compliance Metadata
        public string[] DataCategories { get; set; } = Array.Empty<string>(); // "personal_data", "financial", "health"
        public string[] ProcessingPurposes { get; set; } = Array.Empty<string>(); // "service_provision", "analytics", "security"
        public string? LegalBasis { get; set; } // "consent", "contract", "legal_obligation"
        public string? DataSubjectId { get; set; }
        public string? DataControllerId { get; set; }
        public string? DataProcessorId { get; set; }

        // Security Context
        public string? AuthenticationMethod { get; set; } // "jwt", "api_key", "oauth", "saml"
        public string[] PermissionsUsed { get; set; } = Array.Empty<string>();
        public string[] ScopesUsed { get; set; } = Array.Empty<string>();
        public bool? MfaUsed { get; set; }
        public string? MfaMethod { get; set; }

        // Model-Specific Information
        public string? ModelId { get; set; }
        public string? ModelName { get; set; }
        public string? ModelProvider { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public decimal? Cost { get; set; }
        public string? CostCurrency { get; set; } = "USD";
        public TimeSpan? ProcessingTime { get; set; }

        // Risk & Security
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string? RiskFactors { get; set; } // JSON array
        public bool IsSuspicious { get; set; }
        public string? ThreatIndicator { get; set; }

        // Outcome
        public bool IsSuccess { get; set; } = true;
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }

        // Retention & Compliance
        public DateTime EventTime { get; set; } = DateTime.UtcNow;
        public DateTime LoggedAt { get; set; } = DateTime.UtcNow;
        public int RetentionDays { get; set; } = 730; // Default 2 years for compliance
        public DateTime? ExpiresAt { get; set; }
        public bool IsArchived { get; set; }
        public string? ArchiveLocation { get; set; }

        // Indexes & Search
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string? SearchableText { get; set; }

        // Blockchain Integration (for immutable audit trail)
        public string? BlockchainTransactionHash { get; set; }
        public string? BlockchainBlockNumber { get; set; }
        public string? BlockchainNetwork { get; set; }
        public DateTime? BlockchainTimestamp { get; set; }

        // Relationships
        public virtual ApplicationUser? User { get; set; }
        public virtual Tenant? Tenant { get; set; }
        public virtual Project? Project { get; set; }
    }
}
