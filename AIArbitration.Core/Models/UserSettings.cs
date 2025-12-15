namespace AIArbitration.Core.Models
{
    public class UserSettings
    {
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string? DefaultModel { get; set; }
        public decimal? DefaultTemperature { get; set; }
        public int? DefaultMaxTokens { get; set; }
        public bool AutoSelectModel { get; set; } = true;
        public bool ReceiveNotifications { get; set; } = true;
        public string? NotificationEmail { get; set; }
        public string? TimeZone { get; set; }
        public string? Language { get; set; }
        public Dictionary<string, object> Preferences { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? DefaultTaskType { get; set; }
    }
}
