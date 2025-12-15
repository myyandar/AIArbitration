using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ModelChoice
    {
        public int Index { get; set; }
        public ChatMessage Message { get; set; }
        public string? FinishReason { get; set; }

        // Optional properties that might be useful
        public Delta? Delta { get; set; }
        public string? Text { get; set; }
        public float? LogProbability { get; set; }
        public List<object>? LogProbabilities { get; set; }

        // For compatibility with OpenAI format
        public object? Logprobs { get; set; }
    }
}
