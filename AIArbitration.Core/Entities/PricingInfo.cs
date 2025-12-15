using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class PricingInfo
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelId { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public string PricingModel { get; set; } = string.Empty;
        public decimal? PricePerInputToken { get; set; }
        public decimal? PricePerOutputToken { get; set; }
        public string Currency { get; set; } = "USD";
        public decimal Confidence { get; set; } = 0.9m;
        public decimal? ServiceFee { get; set; }
        public decimal? TaxRate { get; set; }
        public decimal? Discount { get; set; }
        public decimal? InputTokenPrice { get; set; }
        public decimal? OutputTokenPrice { get; set; }
        public decimal? MonthlyFee { get; set; }
        public decimal? SetupFee { get; set; }
        public Dictionary<string, decimal> AdditionalCharges { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; }
        public DateTime EffectiveFrom { get; set; }
        public DateTime EffectiveTo { get; set; }
    }
}
