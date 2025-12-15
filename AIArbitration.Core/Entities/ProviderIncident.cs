using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ProviderIncident
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProviderId { get; set; } = string.Empty;

        // Incident Details
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public IncidentSeverity Severity { get; set; }
        public IncidentStatus Status { get; set; }

        // Impact
        public string ImpactScope { get; set; } = string.Empty; // "all", "specific-models", "region"
        public string AffectedRegions { get; set; } = string.Empty; // JSON array
        public string AffectedModels { get; set; } = string.Empty; // JSON array

        // Timeline
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public DateTime? StartedAt { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }

        // Resolution
        public string? ResolutionNotes { get; set; }
        public string? ResolvedBy { get; set; }

        // Communication
        public bool NotifyUsers { get; set; }
        public string? NotificationMessage { get; set; }

        // Metadata
        public string Source { get; set; } = string.Empty; // "internal", "provider", "statuspage"
        public string? ExternalIncidentId { get; set; }
        public string? ExternalStatusUrl { get; set; }

        // Navigation
        public virtual ModelProvider Provider { get; set; } = null!;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
