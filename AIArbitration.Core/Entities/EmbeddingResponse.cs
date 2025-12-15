using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class EmbeddingResponse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelUsed { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public List<float[]> Embeddings { get; set; } = new();
        public int InputTokens { get; set; }
        public decimal Cost { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string RequestId { get; set; }
        public string Model { get; set; }
        public List<EmbeddingData> Data { get; set; }

        // Optional properties
        public string? Object { get; set; } = "list";
        public string? Usage { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }

    }
}
