namespace AIArbitration.Core.Models
{
    // Arbitration.Core/Models/UsageTrend.cs
    public class UsageTrend
    {
        public DateTime Date { get; set; }
        public int RequestCount { get; set; }
        public int TokenCount { get; set; }
        public decimal Cost { get; set; }
        public int UserCount { get; set; }
        public Dictionary<string, int> RequestsByModel { get; set; } = new();
    }
}
