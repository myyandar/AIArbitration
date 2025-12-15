using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class BatchExecutionResult
    {
        public List<ModelResponse> SuccessfulResponses { get; set; } = new();
        public List<FailedRequest> FailedRequests { get; set; } = new();
        public decimal TotalCost { get; set; }
        public TimeSpan TotalProcessingTime { get; set; }
        public Dictionary<string, int> ModelsUsed { get; set; } = new();

        // Statistics
        public int TotalRequests => SuccessfulResponses.Count + FailedRequests.Count;
        public decimal SuccessRate => TotalRequests > 0 ?
            (decimal)SuccessfulResponses.Count / TotalRequests : 0;

        public decimal AverageCostPerRequest => SuccessfulResponses.Any() ?
            TotalCost / SuccessfulResponses.Count : 0;

        public string BatchId { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public string Error { get; set; }
    }
}
