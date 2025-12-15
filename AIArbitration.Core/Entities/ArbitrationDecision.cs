using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ArbitrationDecision
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string? ApiKeyId { get; set; }
        public string SelectedModelId { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public int CandidateCount { get; set; }
        public TimeSpan SelectionDuration { get; set; }
        public string DecisionFactorsJson { get; set; } = string.Empty;
        public Dictionary<string, object>? AdditionalData { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual AIModel? SelectedModel { get; set; }
        public virtual Tenant? Tenant { get; set; }
        public virtual Project? Project { get; set; }
        public virtual ApplicationUser? User { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorType { get; set; }
    }
}
