using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiSafetySetting
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("threshold")]
        public string Threshold { get; set; } // "BLOCK_NONE", "BLOCK_ONLY_HIGH", "BLOCK_MEDIUM_AND_ABOVE", "BLOCK_LOW_AND_ABOVE"
    }
}

