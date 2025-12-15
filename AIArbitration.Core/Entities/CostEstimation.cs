using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class CostEstimation
    {
        public decimal EstimatedCost { get; set; }
        public decimal InputCost { get; set; }
        public decimal OutputCost { get; set; }
        public int EstimatedInputTokens { get; set; }
        public int EstimatedOutputTokens { get; set; }
        public string Currency { get; set; } = "USD";
        public Dictionary<string, decimal> CostBreakdown { get; set; } = new();

        // Confidence level (0-1)
        public decimal Confidence { get; set; } = 0.8m;

        // Pricing model details
        public string? PricingModel { get; set; }
        public decimal? PricePerInputToken { get; set; }
        public decimal? PricePerOutputToken { get; set; }

        // Additional charges
        public decimal? ServiceFee { get; set; }
        public decimal? Tax { get; set; }
        public decimal? Discount { get; set; }

        // Validation
        public bool IsValid => EstimatedCost >= 0 && Confidence >= 0 && Confidence <= 1;

        public decimal ServiceCost { get; set; }
        public int TotalTokens { get; set; }
        public decimal ServiceFeePercentage { get; set; }
        public string ModelId { get; set; }
        public string ProviderId { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsDetailedEstimation { get; set; }
        public string Notes { get; set; }
        public CostRange ModelRange { get; set; }
        public string ModelName { get; set; }
        public string Provider { get; set; }
        public decimal IntelligenceScore { get; set; }
        public decimal PerformanceScore { get; set; }

        // Helper methods
        public decimal GetTotalTokens() => EstimatedInputTokens + EstimatedOutputTokens;
        public decimal GetCostPerToken() => GetTotalTokens() > 0 ? EstimatedCost / GetTotalTokens() : 0;

    }

    public class ModelRange
    {
        public decimal Minimum { get; set; }
        public decimal Maximum { get; set; }
        public decimal Average { get; set; }
    }
}
