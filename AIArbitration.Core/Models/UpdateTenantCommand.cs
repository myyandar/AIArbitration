using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class UpdateTenantCommand
    {
        public required string TenantId { get; set; }
        public string? Name { get; set; }
        public string? Domain { get; set; }
        public string? CompanyName { get; set; }
        public TenantPlan? Plan { get; set; }
        public BillingCycle? BillingCycle { get; set; }
        public bool? IsActive { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
