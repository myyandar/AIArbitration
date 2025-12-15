using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiContent Content { get; set; }

        [JsonPropertyName("finishReason")]
        public string FinishReason { get; set; }

        [JsonPropertyName("index")]
        public int Index { get; set; }

        [JsonPropertyName("safetyRatings")]
        public List<GeminiSafetyRating> SafetyRatings { get; set; } = new List<GeminiSafetyRating>();
    }
}

