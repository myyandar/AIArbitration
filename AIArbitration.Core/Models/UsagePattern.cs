namespace AIArbitration.Core.Models
{
    // Arbitration.Core/Models/UsagePattern.cs
    public class UsagePattern
    {
        public string PatternType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Confidence { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public List<DateTime> Occurrences { get; set; } = new();
    }
}
