using AIArbitration.Core.Entities;
using System.Text.Json.Serialization;

namespace AIArbitration.Core.Models
{
    // Batch moderation request
    public class BatchModerationRequest
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("requests")]
        public List<ModerationRequest> Requests { get; set; } = new List<ModerationRequest>();

        [JsonPropertyName("config")]
        public ModerationConfiguration Config { get; set; } = new ModerationConfiguration();

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
    }
}

