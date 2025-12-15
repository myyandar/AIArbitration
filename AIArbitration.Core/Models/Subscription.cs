using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class Subscription
    {
        public string TenantId { get; set; } = string.Empty;
        public TenantPlan Plan { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public bool IsActive { get; set; }
        public string? StripeCustomerId { get; set; }
        public string? StripeSubscriptionId { get; set; }
        public string? StripePriceId { get; set; }
        public decimal MonthlyAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public List<SubscriptionFeature> Features { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
