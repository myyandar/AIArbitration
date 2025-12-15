using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Streaming delta
    /// </summary>
    public class StreamingDelta
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public List<ToolCall>? ToolCalls { get; set; }
        public FunctionCall? FunctionCall { get; set; }
        public Dictionary<string, object>? AdditionalProperties { get; set; }
    }
}
