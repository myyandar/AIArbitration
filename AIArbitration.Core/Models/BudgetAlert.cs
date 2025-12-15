using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class BudgetAlert
    {
        public string BudgetId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public Dictionary<string, object> Details { get; set; } = new();
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public bool IsAcknowledged { get; set; }
        public DateTime? AcknowledgedAt { get; set; }
        public string? AcknowledgedBy { get; set; }
    }
}
