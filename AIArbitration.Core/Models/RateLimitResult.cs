using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public string Identifier { get; set; } = string.Empty;
        public RateLimitItem Item { get; set; }
        public int CurrentUsage { get; set; }
        public int Limit { get; set; }
        public DateTime ResetTime { get; set; }
        public TimeSpan RetryAfter { get; set; }
        public string? Message { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public int CurrentCount { get; set; }
        public int MaxCount { get; set; }
        public int Remaining { get; set; }
        public TimeSpan Window { get; set; }
        public RateLimitType Type { get; set; }
    }
}
