using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class AuditLogEntry
    {
        public required string EventType { get; set; }
        public string? EventCategory { get; set; }
        public string? EventSubcategory { get; set; }
        public required string Description { get; set; }

        // Actor Information
        public string? UserId { get; set; }
        public string? UserEmail { get; set; }
        public string? UserName { get; set; }
        public string? TenantId { get; set; }
        public string? TenantName { get; set; }
        public string? ActorType { get; set; }
        public string? ActorId { get; set; }
        public string? ActorName { get; set; }

        // Resource Information
        public string? ResourceType { get; set; }
        public string? ResourceId { get; set; }
        public string? ResourceName { get; set; }
        public string? ResourceAction { get; set; }

        // Data Changes
        public object? OldValues { get; set; }
        public object? NewValues { get; set; }
        public string[]? ChangedFields { get; set; }

        // Compliance
        public string[]? DataCategories { get; set; }
        public string[]? ProcessingPurposes { get; set; }
        public string? LegalBasis { get; set; }
        public string? DataSubjectId { get; set; }

        // Security
        public string[]? PermissionsUsed { get; set; }
        public string[]? ScopesUsed { get; set; }
        public bool? MfaUsed { get; set; }
        public string? MfaMethod { get; set; }

        // Model Information
        public string? ModelId { get; set; }
        public string? ModelName { get; set; }
        public string? ModelProvider { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public decimal? Cost { get; set; }
        public string? CostCurrency { get; set; }
        public TimeSpan? ProcessingTime { get; set; }

        // Risk & Outcome
        public RiskLevel RiskLevel { get; set; } = RiskLevel.Low;
        public string[]? RiskFactors { get; set; }
        public bool IsSuspicious { get; set; }
        public string? ThreatIndicator { get; set; }
        public bool IsSuccess { get; set; } = true;
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public string? StackTrace { get; set; }

        // Context
        public string? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public string? Environment { get; set; }
        public string[]? Tags { get; set; }
        public int RetentionDays { get; set; } = 730;
    }
}
