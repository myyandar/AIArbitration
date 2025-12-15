using AIArbitration.Core.Entities.Enums;
using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Moderation summary for batch operations
    public class ModerationSummary
    {
        [JsonPropertyName("total_requests")]
        public int TotalRequests { get; set; }

        [JsonPropertyName("flagged_requests")]
        public int FlaggedRequests { get; set; }

        [JsonPropertyName("blocked_requests")]
        public int BlockedRequests { get; set; }

        [JsonPropertyName("flagged_percentage")]
        public float FlaggedPercentage { get; set; }

        [JsonPropertyName("most_frequent_category")]
        public string MostFrequentCategory { get; set; }

        [JsonPropertyName("average_severity")]
        public ModerationSeverity AverageSeverity { get; set; }

        [JsonPropertyName("category_breakdown")]
        public Dictionary<string, int> CategoryBreakdown { get; set; } = new Dictionary<string, int>();

        [JsonPropertyName("severity_breakdown")]
        public Dictionary<ModerationSeverity, int> SeverityBreakdown { get; set; } = new Dictionary<ModerationSeverity, int>();
    }
}

