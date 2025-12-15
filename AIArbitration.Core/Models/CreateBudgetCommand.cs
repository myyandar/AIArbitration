using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CreateBudgetCommand
    {
        public required string TenantId { get; set; }
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public decimal Amount { get; set; }
        public BudgetPeriod Period { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal WarningThreshold { get; set; } = 80;
        public decimal CriticalThreshold { get; set; } = 95;
        public bool SendNotifications { get; set; } = true;
    }
}
