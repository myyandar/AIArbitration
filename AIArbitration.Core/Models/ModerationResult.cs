using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class ModerationResult
    {
        [JsonPropertyName("categories")]
        public Dictionary<string, ModerationCategory> Categories { get; set; } = new ();

        [JsonPropertyName("category_scores")]
        public Dictionary<string, decimal> CategoryScores { get; set; } = new Dictionary<string, decimal>();

        [JsonPropertyName("flagged")]
        public bool Flagged { get; set; }

        [JsonPropertyName("confidence")]
        public float Confidence { get; set; }

        [JsonPropertyName("action_required")]
        public bool ActionRequired { get; set; }

        [JsonPropertyName("action_type")]
        public string ActionType { get; set; } = "none"; // none, warn, block, review

        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        [JsonPropertyName("severity")]
        public ModerationSeverity Severity { get; set; } = ModerationSeverity.None;

        [JsonPropertyName("blocked_categories")]
        public List<string> BlockedCategories { get; set; } = new List<string>();

        [JsonPropertyName("warned_categories")]
        public List<string> WarnedCategories { get; set; } = new List<string>();

        [JsonPropertyName("suggested_action")]
        public string SuggestedAction { get; set; }

        [JsonPropertyName("content_analysis")]
        public ContentAnalysis ContentAnalysis { get; set; } = new ContentAnalysis();

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}

