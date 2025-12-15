using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class StreamingCompletion
    {
        public required string ResponseId { get; set; }
        public required string ModelUsed { get; set; }
        public required string Provider { get; set; }
        public required string Content { get; set; }
        public required int InputTokens { get; set; }
        public required int OutputTokens { get; set; }
        public required decimal Cost { get; set; }
        public required FinishReason FinishReason { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

        // For function/tool calling
        public List<ToolCall>? ToolCalls { get; set; }
        public FunctionCall? FunctionCall { get; set; }
        public string StreamId { get; set; }
        public string? SessionId { get; set; }
        public string RequestId { get; set; }
    }
}
