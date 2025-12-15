using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class RateLimitQuota
    {
        public string Identifier { get; set; } = string.Empty;
        public Dictionary<RateLimitItem, RateLimitConfiguration> Configurations { get; set; } = new();
        public int Priority { get; set; } = 1;
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public int RequestLimit { get; set; }
        public TimeSpan RequestWindow { get; set; }
        public int TokenLimit { get; set; }
        public TimeSpan TokenWindow { get; set; }
        public bool IsActive { get; set; }
    }
}
