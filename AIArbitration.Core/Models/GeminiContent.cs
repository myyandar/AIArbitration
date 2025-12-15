using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiPart> Parts { get; set; } = new List<GeminiPart>();

        [JsonPropertyName("role")]
        public string Role { get; set; }
    }
}

