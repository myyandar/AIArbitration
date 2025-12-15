using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ProviderModelCapabilities
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public int ContextWindow { get; set; } = 4096;
        public int MaxOutputTokens { get; set; } = 1024;
        public bool SupportsStreaming { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsAudio { get; set; }
        public bool SupportsDataResidency { get; set; }
        public bool SupportsEncryptionAtRest { get; set; }
        public decimal IntelligenceScore { get; set; } = 70;
        public List<CapabilityType> Capabilities { get; set; } = new();
        public string ProviderId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public int TotalRequests { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public TimeSpan TotalLatency { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan MinLatency { get; set; }
        public TimeSpan MaxLatency { get; set; }
        public decimal SuccessRate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
