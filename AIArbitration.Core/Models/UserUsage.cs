namespace AIArbitration.Core.Models
{
    public class UserUsage
    {
        public string UserId { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TotalRequests { get; set; }
        public int TotalTokens { get; set; }
        public decimal TotalCost { get; set; }
        public decimal AverageCostPerRequest { get; set; }
        public Dictionary<string, int> RequestsByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, int> RequestsByProject { get; set; } = new();
        public Dictionary<string, decimal> CostByProject { get; set; } = new();
    }
}
