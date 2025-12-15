namespace AIArbitration.Core.Models
{
    // Arbitration.Core/Models/CostTrend.cs
    public class CostTrend
    {
        public DateTime Date { get; set; }
        public decimal TotalCost { get; set; }
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();
        public Dictionary<string, decimal> CostByProject { get; set; } = new();
    }
}
