using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiPromptFeedback
    {
        [JsonPropertyName("safetyRatings")]
        public List<GeminiSafetyRating> SafetyRatings { get; set; } = new List<GeminiSafetyRating>();
    }
}

