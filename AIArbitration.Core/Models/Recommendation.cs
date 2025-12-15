namespace AIArbitration.Core.Models
{
    // Arbitration.Core/Models/Recommendation.cs
    public class Recommendation
    {
        public string Area { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Suggestion { get; set; } = string.Empty;
        public decimal ExpectedImpact { get; set; }
        public decimal EffortRequired { get; set; }
        public string Priority { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
    }
}
