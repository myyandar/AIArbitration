using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CreateTenantCommand
    {
        public required string Name { get; set; }
        public required string Domain { get; set; }
        public string? Subdomain { get; set; }
        public required string CompanyName { get; set; }
        public TenantPlan Plan { get; set; } = TenantPlan.Starter;
        public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
        public string? AdminEmail { get; set; }
        public string? AdminName { get; set; }
        public bool SendWelcomeEmail { get; set; } = true;
    }
}
