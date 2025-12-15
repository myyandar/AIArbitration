using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    /// <summary>
    /// Usage statistics for a given period
    /// </summary>
    public class UsageStatistics
    {
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Totals
        public decimal TotalCost { get; set; }
        public int TotalRequests { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }
        public long TotalTokens => TotalInputTokens + TotalOutputTokens;

        // Averages
        public decimal AverageCostPerRequest => TotalRequests > 0 ? TotalCost / TotalRequests : 0;
        public decimal AverageCostPerToken => TotalTokens > 0 ? TotalCost / TotalTokens : 0;
        public decimal AverageInputTokensPerRequest => TotalRequests > 0 ? (decimal)TotalInputTokens / TotalRequests : 0;
        public decimal AverageOutputTokensPerRequest => TotalRequests > 0 ? (decimal)TotalOutputTokens / TotalRequests : 0;

        // Peaks
        public decimal PeakCost { get; set; }
        public DateTime PeakCostTime { get; set; }
        public int PeakRequestRate { get; set; } // Requests per hour
        public DateTime PeakRequestRateTime { get; set; }

        // Trends
        public decimal CostTrendPercentage { get; set; } // Compared to previous period
        public int RequestTrendPercentage { get; set; }
        public long TokenTrendPercentage { get; set; }

        // Breakdowns
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public Dictionary<string, decimal> CostByProject { get; set; } = new();
        public Dictionary<string, decimal> CostByUser { get; set; } = new();

        // Time series data (hourly/daily breakdown)
        public Dictionary<DateTime, decimal> HourlyCosts { get; set; } = new();
        public Dictionary<DateTime, int> HourlyRequests { get; set; } = new();

        // Efficiency metrics
        public decimal CostEfficiencyScore { get; set; } // 0-100 scale
        public Dictionary<string, decimal> ModelEfficiencyScores { get; set; } = new();
    }
}
