using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CircuitEvent
    {
        public string CircuitName { get; set; } = string.Empty;
        public CircuitEventType EventType { get; set; }
        public DateTime EventTime { get; set; }
        public string? Details { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
