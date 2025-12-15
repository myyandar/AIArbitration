using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CircuitInfo
    {
        public string CircuitName { get; set; } = string.Empty;
        public CircuitStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastStateChange { get; set; }
        public int TotalRequests { get; set; }
        public int FailedRequests { get; set; }
        public decimal FailureRate => TotalRequests > 0 ? (decimal)FailedRequests / TotalRequests * 100 : 0;
        public TimeSpan AverageResponseTime { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
