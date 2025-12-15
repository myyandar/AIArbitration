namespace AIArbitration.Core.Entities
{
    // Add AuditTrail for comprehensive logging
    public class AuditTrail
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty; // "request_sent", "response_received", "rule_evaluated"
        public string ResourceType { get; set; } = string.Empty; // "model", "request", "response"
        public string? ResourceId { get; set; }
        public string? UserId { get; set; }
        public string? IpAddress { get; set; }
        public string? UserAgent { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Tenant Tenant { get; set; } = null!;
    }

}
