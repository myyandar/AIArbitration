namespace AIArbitration.Core.Models
{
    // Arbitration.Core/Models/Anomaly.cs
    public class Anomaly
    {
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Severity { get; set; }
        public DateTime DetectedAt { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new();
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
