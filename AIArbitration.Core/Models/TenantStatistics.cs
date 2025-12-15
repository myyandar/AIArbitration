using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class TenantStatistics
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public TenantPlan CurrentPlan { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public bool IsActive { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TotalProjects { get; set; }
        public int ActiveProjects { get; set; } 
        public int TotalApiKeys { get; set; }
        public decimal MonthlyAverageCost { get; set; }
        public decimal LifetimeCost { get; set; }
        public Dictionary<string, int> ModelUsageCount { get; set; } = new();
        public Dictionary<string, int> ProviderUsageCount { get; set; } = new();
    }
}
