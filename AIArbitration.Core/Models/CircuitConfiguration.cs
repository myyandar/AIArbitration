namespace AIArbitration.Core.Models
{
    public class CircuitConfiguration
    {
        public string CircuitName { get; set; } = string.Empty;
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);
        public int MinimumThroughput { get; set; } = 10;
        public TimeSpan DurationOfBreak { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public List<Type> HandledExceptions { get; set; } = new();
        public bool IsEnabled { get; set; } = true;
        public Dictionary<string, object> AdvancedSettings { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
