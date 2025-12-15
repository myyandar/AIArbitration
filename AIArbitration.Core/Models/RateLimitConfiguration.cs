using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class RateLimitConfiguration
    {
        public string Identifier { get; set; } = string.Empty;
        public RateLimitItem Item { get; set; }
        public int Limit { get; set; }
        public TimeSpan Period { get; set; }
        public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.FixedWindow;
        public bool Enabled { get; set; } = true;
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public RateLimitType Type { get; set; }
        public int MaxRequests { get; set; }
        public TimeSpan Window { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsEnabled { get; set; } = true;
    }
}
