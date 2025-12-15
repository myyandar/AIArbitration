using AIArbitration.Core.Entities.Enums;
using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Custom moderation rule
    public class ModerationRule
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("pattern")]
        public string Pattern { get; set; } = string.Empty;

        [JsonPropertyName("pattern_type")]
        public string PatternType { get; set; } = "regex"; // regex, keyword, phrase

        [JsonPropertyName("action")]
        public string Action { get; set; } = "warn"; // warn, block, flag, notify

        [JsonPropertyName("severity")]
        public ModerationSeverity Severity { get; set; } = ModerationSeverity.Medium;

        [JsonPropertyName("category")]
        public string Category { get; set; } = "custom";

        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updated_at")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

