using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiPart
    {
        [JsonPropertyName("text")]
        public string Text { get; set; }
    }
}

