namespace AIArbitration.Core.Entities
{
    // Compliance log for tracking
    public class ComplianceLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RuleId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public ComplianceCheckResult Result { get; set; } = null!;
        public string ResourceType { get; set; } = string.Empty;
        public string? ResourceId { get; set; }
        public string? UserId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ComplianceRule Rule { get; set; } = null!;
        public virtual Tenant Tenant { get; set; } = null!;
    }
}