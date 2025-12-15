using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CircuitState
    {
        public static CircuitState Closed { get; set; }
        public static CircuitState Open { get; set; }
        public static CircuitState HalfOpen { get; set; }
        public string CircuitName { get; set; } = string.Empty;
        public CircuitStatus Status { get; set; }
        public int FailureCount { get; set; }
        public int SuccessCount { get; set; }
        public DateTime? LastFailureTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public DateTime? OpenedTime { get; set; }
        public DateTime? HalfOpenTime { get; set; }
        public TimeSpan? TimeUntilRetry { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
