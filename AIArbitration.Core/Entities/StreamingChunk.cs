using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class StreamingChunk
    {
        public required string Id { get; set; }
        public string? Model { get; set; }
        public string? Content { get; set; }
        public bool IsFirstChunk { get; set; }
        public bool IsLastChunk { get; set; }
        public FinishReason? FinishReason { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
        public FunctionCall? FunctionCall { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Token information (if available during streaming)
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }

        // Provider-specific data
        public Dictionary<string, object>? ProviderData { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public string StreamId { get; set; }
        public string RequestId { get; set; }
        public string Provider { get; set; }
    }
}
