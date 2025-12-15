using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ApplicationUser
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string HashedPassword { get; set; } = string.Empty;
        public string Salt { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }

        // Security
        public bool IsActive { get; set; } = true;
        public bool EmailVerified { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public string? TwoFactorSecret { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public int FailedLoginAttempts { get; set; }

        // Tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public string? LastLoginIP { get; set; }
        public string? LastLoginUserAgent { get; set; }

        // Foreign keys
        public string? TenantId { get; set; }
        public string? RoleId { get; set; }

        // Navigation properties
        public virtual Tenant? Tenant { get; set; }
        public virtual UserRole? Role { get; set; }
        public virtual ICollection<ArbitrationDecision> ArbitrationDecisions { get; set; } = new List<ArbitrationDecision>();
        public virtual ICollection<UserSession> Sessions { get; set; } = new List<UserSession>();
        public virtual ICollection<ApiKey> ApiKeys { get; set; } = new List<ApiKey>();
        public virtual ICollection<UserPermission> Permissions { get; set; } = new List<UserPermission>();
        public virtual ICollection<BudgetAllocation> Budgets { get; set; } = new List<BudgetAllocation>();
        public virtual ICollection<CostRecord> CostRecords { get; set; } = new List<CostRecord>(); // Add this
    }
}

