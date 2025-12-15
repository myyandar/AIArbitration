using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class StreamingModelResponse
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelUsed { get; set; }
        public string Provider { get; set; }
        public IAsyncEnumerable<StreamingChunk> Stream { get; set; }

        // Will be populated after stream completion
        public string? FinalContent { get; set; }
        public int? FinalInputTokens { get; set; }
        public int? FinalOutputTokens { get; set; }
        public decimal? FinalCost { get; set; }
        public FinishReason? FinalFinishReason { get; set; }

        // Callback to get completion details
        public required Func<Task<StreamingCompletion>> GetCompletionAsync { get; set; }

        // Request context
        public string? RequestId { get; set; }
        public string? SessionId { get; set; }
        public DateTime StartTime { get; set; }
        public string Error { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Func<object, object, object, Task> OnCompletion { get; set; }
        public string ModelId { get; set; }
    }
}
