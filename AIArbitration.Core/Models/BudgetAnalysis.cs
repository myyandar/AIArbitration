namespace AIArbitration.Core.Models
{
    public class BudgetAnalysis
    {
        public string BudgetId { get; set; } = string.Empty;
        public DateTime AnalysisPeriodStart { get; set; }
        public DateTime AnalysisPeriodEnd { get; set; }
        public decimal TotalUsage { get; set; }
        public decimal BudgetAmount { get; set; }
        public decimal UsagePercentage => BudgetAmount > 0 ? (TotalUsage / BudgetAmount) * 100 : 0;
        public int TotalRequests { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public Dictionary<string, decimal> UsageByDay { get; set; } = new();
        public Dictionary<string, decimal> UsageByModel { get; set; } = new();
        public Dictionary<string, decimal> UsageByProject { get; set; } = new();
        public Dictionary<string, decimal> UsageByUser { get; set; } = new();
        public List<UsagePattern> UsagePatterns { get; set; } = new();
        public List<Anomaly> Anomalies { get; set; } = new();
        public List<Recommendation> Recommendations { get; set; } = new();
    }
}
