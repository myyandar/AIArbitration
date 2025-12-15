namespace AIArbitration.Core.Models
{
    public class ModelInfo
    {
        public string ModelId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int MaxTokens { get; set; }
        public decimal CostPerMillionInputTokens { get; set; }
        public decimal CostPerMillionOutputTokens { get; set; }
        public int IntelligenceScore { get; set; }
        public string Tier { get; set; } = string.Empty;
        public List<string> Capabilities { get; set; } = new();
        public string ProviderModelId { get; set; }
    }
}
