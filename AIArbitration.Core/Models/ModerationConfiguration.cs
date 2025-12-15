using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Moderation configuration
    public class ModerationConfiguration
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("provider")]
        public string Provider { get; set; } = "openai";

        [JsonPropertyName("model")]
        public string Model { get; set; } = "text-moderation-latest";

        [JsonPropertyName("thresholds")]
        public Dictionary<string, float> Thresholds { get; set; } = new Dictionary<string, float>
        {
            ["hate"] = 0.7f,
            ["hate/threatening"] = 0.7f,
            ["self-harm"] = 0.7f,
            ["sexual"] = 0.7f,
            ["sexual/minors"] = 0.7f,
            ["violence"] = 0.7f,
            ["violence/graphic"] = 0.7f
        };

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>
        {
            "hate",
            "hate/threatening",
            "self-harm",
            "sexual",
            "sexual/minors",
            "violence",
            "violence/graphic"
        };

        [JsonPropertyName("auto_moderate")]
        public bool AutoModerate { get; set; } = true;

        [JsonPropertyName("block_on_violation")]
        public bool BlockOnViolation { get; set; } = false;

        [JsonPropertyName("log_violations")]
        public bool LogViolations { get; set; } = true;

        [JsonPropertyName("notify_on_violation")]
        public bool NotifyOnViolation { get; set; } = false;

        [JsonPropertyName("notify_emails")]
        public List<string> NotifyEmails { get; set; } = new List<string>();

        [JsonPropertyName("custom_rules")]
        public List<ModerationRule> CustomRules { get; set; } = new List<ModerationRule>();
    }
}

