using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiGenerationConfig
    {
        [JsonPropertyName("temperature")]
        public float Temperature { get; set; } = 0.7f;

        [JsonPropertyName("topP")]
        public float TopP { get; set; } = 0.95f;

        [JsonPropertyName("topK")]
        public int TopK { get; set; } = 40;

        [JsonPropertyName("maxOutputTokens")]
        public int MaxOutputTokens { get; set; } = 2048;
    }
}

