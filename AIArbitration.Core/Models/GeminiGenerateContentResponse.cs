using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate> Candidates { get; set; } = new List<GeminiCandidate>();

        [JsonPropertyName("promptFeedback")]
        public GeminiPromptFeedback PromptFeedback { get; set; }

        [JsonPropertyName("usageMetadata")]
        public GeminiUsageMetadata UsageMetadata { get; set; }
    }
}

