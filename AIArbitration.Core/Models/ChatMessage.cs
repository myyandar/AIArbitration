using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class ChatMessage
    {
        public required string Role { get; set; } // "system", "user", "assistant", "tool", "function"
        public string? Name { get; set; } // For function/tool calls

        // For function/tool responses
        public string? ToolCallId { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
        public FunctionCall? FunctionCall { get; set; }

        // Metadata
        public Dictionary<string, object>? Metadata { get; set; }
        public List<FunctionCall>? FunctionCalls { get; set; }
        public string Content { get; set; } = string.Empty;

        // Optional properties for complex message formats
        public List<ContentPart>? ContentParts { get; set; }
    }
}
