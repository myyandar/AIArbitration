using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class RateLimitViolation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Identifier { get; set; } = string.Empty;
        public RateLimitItem Item { get; set; }
        public int AttemptedUsage { get; set; }
        public int Limit { get; set; }
        public string? IPAddress { get; set; }
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string? Endpoint { get; set; }
        public DateTime ViolationTime { get; set; } = DateTime.UtcNow;
        public bool IsBlocked { get; set; }
        public DateTime? BlockedUntil { get; set; }
        public RateLimitType Type { get; set; }
        public int CurrentCount { get; set; }
        public int MaxCount { get; set; }
        public DateTime ViolatedAt { get; set; }
        public DateTime ResetTime { get; set; }
        public DateTime RecordedAt { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}
