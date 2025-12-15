using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class AIModel
    {
        // AIArbitration.Core/AIModels/AIModel.cs
        public string Id { get; set; } = Guid.NewGuid().ToString();
        // "gpt-4", "claude-3", etc.
        public string ProviderModelId { get; set; } = string.Empty; 
        // internal name
        public string Name { get; set; } = string.Empty; 
        public string DisplayName { get; set; } = string.Empty;
        public decimal IntelligenceScore { get; set; }
        public decimal CostPerMillionInputTokens { get; set; }
        public decimal CostPerMillionOutputTokens { get; set; }
        public int ContextWindow { get; set; }
        public int MaxOutputTokens { get; set; }
        public ModelTier Tier { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsDataResidency { get; set; }
        public bool SupportsEncryptionAtRest { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public double Latency { get; set; } // first token latency in seconds
        public ModelTier ModelTier { get; set; }
        // Foreign key
        public string ProviderId { get; set; } = string.Empty;
        public virtual ModelProvider Provider { get; set; } = null!;
        public string PricinginfoId { get; set; } = string.Empty;
        public virtual PricingInfo PricingInfo { get; set; } = null!;
        public string Description { get; set; }
        public bool? SupportsFunctionCalling { get; set; }
        public bool? SupportsVision { get; set; }
        public bool? SupportsAudio { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime DeprecationDate { get; set; }
        public string ReplacementModelId { get; set; }

        // ModelCapabilities
        public virtual ICollection<ModelCapability> Capabilities { get; set; } = new List<ModelCapability>();
        public virtual ICollection<PerformancePrediction> PerformanceMetrics { get; set; } = new List<PerformancePrediction>();
        public string PricingInfoId { get; set; } = string.Empty;
        public List<string>? DataResidencyRegions { get; set; }
        public int MaxTokens { get; set; }
    }
}
