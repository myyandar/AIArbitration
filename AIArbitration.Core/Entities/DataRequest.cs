using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class DataRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DataRequestType Type { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string? TenantId { get; set; }
        public string? ProjectId { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? Deadline { get; set; } // Legal deadline for response
        public Dictionary<string, object> Parameters { get; set; } = new();
        public string Status { get; set; } = "pending"; // pending, processing, completed, failed
        public string? Notes { get; set; }
        public string? VerificationToken { get; set; }

        public virtual ApplicationUser User { get; set; } = null!;
    }
}
