namespace AIArbitration.Core.Models
{
    public class TenantSettings
    {
        public string TenantId { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? CompanyLogoUrl { get; set; }
        public string? PrimaryColor { get; set; }
        public string? SecondaryColor { get; set; }
        public string? TimeZone { get; set; }
        public string? DateFormat { get; set; }
        public string? Language { get; set; }
        public bool EnableAuditLogging { get; set; } = true;
        public int AuditLogRetentionDays { get; set; } = 730;
        public bool EnableUsageAlerts { get; set; } = true;
        public Dictionary<string, object> CustomSettings { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
