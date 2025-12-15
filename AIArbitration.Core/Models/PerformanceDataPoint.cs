using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class PerformanceDataPoint
    {
        public string ModelId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan Latency { get; set; }
        public bool Success { get; set; }
        public decimal Cost { get; set; }
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public string TaskType { get; set; } = string.Empty;
        public string? Region { get; set; }
        public Dictionary<string, object> Context { get; set; } = new();
    }
}
