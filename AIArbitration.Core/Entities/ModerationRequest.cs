using System.Text.Json.Serialization;

namespace AIArbitration.Core.Entities
{
    public class ModerationRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("content")]
        public string Content { get; set; } = string.Empty;

        [JsonPropertyName("content_type")]
        public string ContentType { get; set; } = "text"; // text, image, audio, video

        [JsonPropertyName("content_format")]
        public string ContentFormat { get; set; } = "plain"; // plain, html, markdown, json

        [JsonPropertyName("language")]
        public string Language { get; set; } = "en";

        [JsonPropertyName("categories")]
        public List<string> Categories { get; set; } = new List<string>();

        [JsonPropertyName("thresholds")]
        public Dictionary<string, float> Thresholds { get; set; } = new Dictionary<string, float>
        {
            ["hate"] = 0.7f,
            ["hate/threatening"] = 0.7f,
            ["self-harm"] = 0.7f,
            ["sexual"] = 0.7f,
            ["sexual/minors"] = 0.7f,
            ["violence"] = 0.7f,
            ["violence/graphic"] = 0.7f
        };

        [JsonPropertyName("strict_mode")]
        public bool StrictMode { get; set; } = false;

        [JsonPropertyName("return_scores")]
        public bool ReturnScores { get; set; } = true;

        [JsonPropertyName("return_metadata")]
        public bool ReturnMetadata { get; set; } = false;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; }

        [JsonPropertyName("tenant_id")]
        public string TenantId { get; set; }

        [JsonPropertyName("project_id")]
        public string ProjectId { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
        public string ModelId { get; set; } = string.Empty;
        public AIModel Model { get; set; } 
        public string ProviderID { get; set; } = string.Empty;
        public ModelProvider Provider { get; set; }
        /// <summary>
        /// The input text or content to be moderated
        /// </summary>
        public string Input { get; set; }

        /// <summary>
        /// Optional: Request identifier for tracing
        /// </summary>
        public string RequestId { get; set; }
        // Additional properties for different providers
        public ModerationOptions Options { get; set; } = new ModerationOptions();

    }

    public class ModerationOptions
    {
        public bool? ReturnScores { get; set; }
        public bool? ReturnCategories { get; set; }
        public bool? ReturnDetailedScores { get; set; }
    }
}
