using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class ComplianceViolation
    {
        public string ViolationId { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public ComplianceStandard Standard { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ViolationSeverity Severity { get; set; }
        public string? EntityId { get; set; }
        public string? EntityType { get; set; }
        public DateTime DetectedAt { get; set; }
        public bool IsResolved { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolutionNotes { get; set; }
    }
}
