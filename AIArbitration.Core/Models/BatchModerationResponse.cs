using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Batch moderation response
    public class BatchModerationResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("responses")]
        public List<ModerationResponse> Responses { get; set; } = new List<ModerationResponse>();

        [JsonPropertyName("summary")]
        public ModerationSummary Summary { get; set; } = new ModerationSummary();

        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("processing_time")]
        public TimeSpan ProcessingTime { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }

        [JsonPropertyName("total_cost")]
        public decimal TotalCost { get; set; }

        [JsonPropertyName("success")]
        public bool Success { get; set; } = true;

        [JsonPropertyName("error_message")]
        public string ErrorMessage { get; set; }
    }
}

