using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class BudgetNotification
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string BudgetId { get; set; } = string.Empty;
        public BudgetNotificationType Type { get; set; }
        public string RecipientEmail { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }

        // Navigation
        public virtual BudgetAllocation Budget { get; set; } = null!;
    }
}
