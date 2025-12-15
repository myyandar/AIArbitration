namespace AIArbitration.Core.Entities
{
    // Helper class for cost aggregation
    public class CostAggregation
    {
        public string TenantId { get; set; } = string.Empty;
        public string Period { get; set; } = string.Empty; // "daily", "weekly", "monthly", "quarterly"
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalTax { get; set; }
        public decimal TotalDiscount { get; set; }
        public decimal NetAmount => TotalAmount + TotalTax - TotalDiscount;
        public int TotalRequests { get; set; }
        public long TotalTokens { get; set; }
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public Dictionary<string, decimal> CostByProject { get; set; } = new();
        public Dictionary<string, decimal> CostByUser { get; set; } = new();
    }
}
