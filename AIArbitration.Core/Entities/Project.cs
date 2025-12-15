using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class Project
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string? OwnerId { get; set; }

        // Configuration
        public decimal MonthlyBudget { get; set; }
        public decimal DailyBudget { get; set; }
        public string? AllowedModels { get; set; } // JSON array of allowed model IDs
        public string? BlockedModels { get; set; } // JSON array of blocked model IDs
        public string? RateLimitConfig { get; set; } // JSON configuration

        // Tracking
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ApplicationUser? Owner { get; set; }
        public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
        public virtual ICollection<BudgetAllocation> Budgets { get; set; } = new List<BudgetAllocation>();
        public virtual ICollection<CostRecord> CostRecords { get; set; } = new List<CostRecord>(); // Add this
        public virtual ICollection<ArbitrationDecision> ArbitrationDecisions { get; set; } = new List<ArbitrationDecision>();
    }
}
