using System;
using System.Collections.Generic;
using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class OpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Name { get; set; }
        public OpenAiFunctionCall? FunctionCall { get; set; }
        public List<OpenAiToolCall>? ToolCalls { get; set; }
    }
}
