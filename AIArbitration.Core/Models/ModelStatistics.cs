using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class ModelStatistics
    {
        public string ModelId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public decimal SuccessRate => TotalRequests > 0 ? (decimal)SuccessfulRequests / TotalRequests * 100 : 0;
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public TimeSpan P99Latency { get; set; }
        public int AverageInputTokens { get; set; }
        public int AverageOutputTokens { get; set; }
        public Dictionary<string, int> UsageByTenant { get; set; } = new();
        public Dictionary<string, decimal> CostByTenant { get; set; } = new();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public double AverageSuccessRate { get; set; }
    }
}
