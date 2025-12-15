using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    // Cost estimation request
    public class CostEstimationRequest
    {
        public string ModelId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public int EstimatedInputTokens { get; set; }
        public int EstimatedOutputTokens { get; set; }
        public int EstimatedRequests { get; set; } = 1;
        public string? Region { get; set; }
        public string? Tier { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = new();
    }
}
