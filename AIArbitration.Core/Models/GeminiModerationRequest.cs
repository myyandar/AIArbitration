using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Gemini moderation request DTO
    public class GeminiModerationRequest
    {
        [JsonPropertyName("contents")]
        public List<GeminiContent> Contents { get; set; } = new List<GeminiContent>();

        [JsonPropertyName("safetySettings")]
        public List<GeminiSafetySetting> SafetySettings { get; set; } = new List<GeminiSafetySetting>();

        [JsonPropertyName("generationConfig")]
        public GeminiGenerationConfig GenerationConfig { get; set; }
    }
}

