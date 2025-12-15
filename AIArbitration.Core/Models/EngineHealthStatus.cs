namespace AIArbitration.Core.Models
{
    public class EngineHealthStatus
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "Unknown";
        public Dictionary<string, bool> ComponentHealth { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    }
}
