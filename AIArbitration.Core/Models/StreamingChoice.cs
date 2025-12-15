using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Streaming choice
    /// </summary>
    public class StreamingChoice
    {
        public int Index { get; set; }
        public StreamingDelta Delta { get; set; } = new();
        public FinishReason? FinishReason { get; set; }
        public Dictionary<string, object>? Logprobs { get; set; }
    }
}
