using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    // AIArbitration.Core/AIModels/ModelCapability.cs
    public class ModelCapability
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
        public string ModelId { get; set; } = string.Empty;
        public virtual AIModel Model { get; set; } = null!;
        public CapabilityType CapabilityType { get; set; }
        public decimal Score { get; set; } // 0-100 scale
        public string? Description { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}
