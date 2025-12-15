using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class RateLimitUsage
    {
        public string Identifier { get; set; } = string.Empty;
        public RateLimitItem Item { get; set; }
        public int CurrentUsage { get; set; }
        public int Limit { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public RateLimitType Type { get; set; }
        public int CurrentCount { get; set; }
        public int MaxCount { get; set; }
        public int Remaining { get; set; }
        public decimal PercentageUsed { get; set; }
        public DateTime ResetTime { get; set; }
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
