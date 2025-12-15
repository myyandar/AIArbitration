namespace AIArbitration.Core.Entities
{
    // Add ConsentRecord for managing user consents
    public class ConsentRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string ConsentType { get; set; } = string.Empty; // "privacy_policy", "terms_of_service", "data_processing", "marketing"
        public string Version { get; set; } = string.Empty;
        public bool Given { get; set; }
        public DateTime GivenAt { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public DateTime? RevokedAt { get; set; }
        public string? RevokedReason { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();

        public virtual ApplicationUser User { get; set; } = null!;
        public virtual Tenant Tenant { get; set; } = null!;
    }
}
