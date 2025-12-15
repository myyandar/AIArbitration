using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public enum FinishReason
    {
        Unknown = 0,
        Stop,           // API returned complete message
        Length,         // Incomplete due to max_tokens parameter or token limit
        ContentFilter,  // Omitted content due to a flag from our content filters
        ToolCalls,      // Model decided to call a tool/function
        Error,          // Error occurred during generation
        Cancelled,      // Request was cancelled
        Timeout,        // Request timed out
        Interrupted,    // Generation was interrupted
        ProviderLimit   // Provider rate limit or quota exceeded
    }
}
