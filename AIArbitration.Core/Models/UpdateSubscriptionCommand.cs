using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class UpdateSubscriptionCommand
    {
        public required string TenantId { get; set; }
        public TenantPlan Plan { get; set; }
        public BillingCycle? BillingCycle { get; set; }
        public bool IsActive { get; set; }
        public DateTime? EndDate { get; set; }
        public DateTime? StartDate { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
