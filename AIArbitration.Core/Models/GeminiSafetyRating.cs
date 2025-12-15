using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    public class GeminiSafetyRating
    {
        [JsonPropertyName("category")]
        public string Category { get; set; }

        [JsonPropertyName("probability")]
        public string Probability { get; set; } // "NEGLIGIBLE", "LOW", "MEDIUM", "HIGH"

        [JsonPropertyName("probabilityScore")]
        public float ProbabilityScore { get; set; }

        [JsonPropertyName("severity")]
        public string Severity { get; set; } // "NEGLIGIBLE", "LOW", "MEDIUM", "HIGH"

        [JsonPropertyName("severityScore")]
        public float SeverityScore { get; set; }
    }
}

