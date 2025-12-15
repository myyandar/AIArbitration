namespace AIArbitration.Core.Models
{
    public class TenantAnalytics
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; }
        public int TotalRequests { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<string, int> UserActivity { get; set; } = new();
        public Dictionary<string, int> ProjectActivity { get; set; } = new();
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public List<UsageTrend> UsageTrends { get; set; } = new();
        public List<CostTrend> CostTrends { get; set; } = new();
        public DateTime CreatedAt { get; set; } 
    }
}
