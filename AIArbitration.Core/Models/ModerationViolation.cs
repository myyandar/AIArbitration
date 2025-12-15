using AIArbitration.Core.Entities.Enums;
using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Moderation violation record
    public class ModerationViolation
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("request_id")]
        public string RequestId { get; set; }

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("content_preview")]
        public string ContentPreview { get; set; }

        [JsonPropertyName("violated_categories")]
        public List<string> ViolatedCategories { get; set; } = new List<string>();

        [JsonPropertyName("category_scores")]
        public Dictionary<string, float> CategoryScores { get; set; } = new Dictionary<string, float>();

        [JsonPropertyName("severity")]
        public ModerationSeverity Severity { get; set; }

        [JsonPropertyName("action_taken")]
        public string ActionTaken { get; set; } // blocked, warned, flagged, allowed

        [JsonPropertyName("moderation_result")]
        public ModerationResult ModerationResult { get; set; }

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("reviewed")]
        public bool Reviewed { get; set; } = false;

        [JsonPropertyName("reviewed_by")]
        public string ReviewedBy { get; set; }

        [JsonPropertyName("reviewed_at")]
        public DateTime ReviewedAt { get; set; }

        [JsonPropertyName("review_notes")]
        public string ReviewNotes { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}

