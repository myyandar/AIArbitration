using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class BudgetAllocation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }

        // Budget configuration
        public BudgetPeriod Period { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime CreatedAt { get; set; }

        // Notifications
        public decimal WarningThreshold { get; set; } = 0.8m; // 80%
        public decimal CriticalThreshold { get; set; } = 0.95m; // 95%
        public bool SendNotifications { get; set; } = true;

        // Tracking
        public decimal UsedAmount { get; set; }
        public decimal RemainingAmount => Amount - UsedAmount;
        public decimal UsagePercentage => Amount > 0 ? UsedAmount / Amount : 0;
        public bool IsExhausted => RemainingAmount <= 0;

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual Project? Project { get; set; }
        public virtual ApplicationUser? User { get; set; }
        public virtual ICollection<BudgetNotification> Notifications { get; set; } = new List<BudgetNotification>();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public decimal Threshold { get; set; }
        public decimal CurrentUsage { get; set; }
        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Recipient info
        public string RecipientEmail { get; set; } = string.Empty;
        public string? RecipientUserId { get; set; }
        public DateTime LastUpdated { get; set; } 
    }
}
