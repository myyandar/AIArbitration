using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class ComplianceConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public List<ComplianceStandard> EnabledStandards { get; set; } = new();
        public string? DefaultDataRegion { get; set; }
        public bool EnableAuditTrail { get; set; } = true;
        public int AuditRetentionDays { get; set; } = 730;
        public bool EnableDataEncryption { get; set; } = true;
        public bool RequireConsent { get; set; } = true;
        public string? ConsentVersion { get; set; }
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
