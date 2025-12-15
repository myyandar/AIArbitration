using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class UpdateBudgetCommand
    {
        public required string BudgetId { get; set; }
        public required string TenantId { get; set; }
        public string ProjectId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public BudgetPeriod? Period { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal? WarningThreshold { get; set; }
        public string Currency { get; set; } 
        public decimal? CriticalThreshold { get; set; }
        public bool? SendNotifications { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
