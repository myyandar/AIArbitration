using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class ModelQuery
    {
        public List<string>? ProviderIds { get; set; }
        public List<ModelTier>? Tiers { get; set; }
        public decimal? MinIntelligenceScore { get; set; }
        public decimal? MaxCostPerMillionTokens { get; set; }
        public int? MinContextWindow { get; set; }
        public bool? SupportsStreaming { get; set; }
        public List<CapabilityType>? RequiredCapabilities { get; set; }
        public bool? IsActive { get; set; } = true;
        public int? Limit { get; set; }
        public int? Offset { get; set; }
        public string? SortBy { get; set; }
        public bool? SortDescending { get; set; }
        public string? ProviderId { get; set; }
        public ModelTier? Tier { get; set; }
        public CapabilityType? Capability { get; set; }
        public decimal? MaxCostPerMillionInputTokens { get; set; }
        public decimal? MaxCostPerMillionOutputTokens { get; set; }
        public bool? SupportsFunctionCalling { get; set; }
        public bool? SupportsVision { get; set; }
        public bool? SupportsAudio { get; set; }
        public DateTime? LastUpdatedAfter { get; set; }
        public decimal? MaxCostPerMillionInput { get; set; }
        public decimal? MaxCostPerMillionOutput { get; set; }
    }
}
