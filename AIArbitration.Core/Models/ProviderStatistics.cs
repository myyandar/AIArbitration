namespace AIArbitration.Core.Models
{
    public class ProviderStatistics
    {
        public string ProviderId { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public decimal SuccessRate => TotalRequests > 0 ? (decimal)SuccessfulRequests / TotalRequests * 100 : 0;
        public decimal TotalCost { get; set; }
        public decimal UptimePercentage { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public int ActiveModels { get; set; }
        public Dictionary<string, int> ModelUsage { get; set; } = new();
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int ActiveModelsCount { get; set; }
        public double AverageSuccessRate { get; set; }
    }
}
