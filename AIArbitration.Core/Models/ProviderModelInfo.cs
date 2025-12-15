using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class ProviderModelInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string ModelName { get; set; } = string.Empty;
        public string ProviderName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int ContextWindow { get; set; }
        public int MaxOutputTokens { get; set; }
        public bool SupportsStreaming { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsAudio { get; set; }
        public IEnumerable<ModelCapability> Capabilities { get; set; } = Array.Empty<ModelCapability>();
        public PricingInfo Pricing { get; set; } = new PricingInfo();
        public DateTime LastUpdated { get; set; }
        public string DisplayName { get; set; }
        public DateTime DeprecationDate { get; set; }
        public string Provider { get; set; }
        public bool IsActive { get; set; }
    }
}
