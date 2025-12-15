using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Content analysis details
    public class ContentAnalysis
    {
        [JsonPropertyName("contains_sensitive_content")]
        public bool ContainsSensitiveContent { get; set; }

        [JsonPropertyName("contains_pii")]
        public bool ContainsPii { get; set; }

        [JsonPropertyName("pii_types")]
        public List<string> PiiTypes { get; set; } = new List<string>();

        [JsonPropertyName("contains_toxicity")]
        public bool ContainsToxicity { get; set; }

        [JsonPropertyName("toxicity_score")]
        public float ToxicityScore { get; set; }

        [JsonPropertyName("sentiment")]
        public string Sentiment { get; set; } = "neutral"; // positive, negative, neutral

        [JsonPropertyName("sentiment_score")]
        public float SentimentScore { get; set; }

        [JsonPropertyName("topics")]
        public List<string> Topics { get; set; } = new List<string>();

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";

        [JsonPropertyName("language_confidence")]
        public float LanguageConfidence { get; set; }
    }
}

