using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class Tenant
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Subdomain { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string CompanyLogoUrl { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;

        // Billing & Plans
        public TenantPlan Plan { get; set; }
        public BillingCycle BillingCycle { get; set; }
        public string? StripeCustomerId { get; set; }
        public string? StripeSubscriptionId { get; set; }
        public DateTime SubscriptionStart { get; set; }
        public DateTime? SubscriptionEnd { get; set; }
        public bool IsActive { get; set; } = true;

        // Configuration
        public string DefaultRegion { get; set; } = "us-east-1";
        public string? ComplianceRegions { get; set; } // JSON array
        public string? AllowedIPRanges { get; set; } // JSON array for IP whitelisting
        public string? BlockedIPRanges { get; set; } // JSON array for IP blacklisting

        // Limits
        public int MaxUsers { get; set; } = 10;
        public int MaxApiKeys { get; set; } = 5;
        public int MaxProjects { get; set; } = 3;
        public decimal MonthlyBudget { get; set; } = 1000.00m;

        // Tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? TrialEndsAt { get; set; }
        public BillingAddress? BillingAddress { get; set; }
        public ContactInfo? ContactInfo { get; set; }
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
        public virtual ICollection<Project> Projects { get; set; } = new List<Project>();
        public virtual ICollection<TenantSetting> Settings { get; set; } = new List<TenantSetting>();
        public virtual ICollection<ComplianceRule> ComplianceRules { get; set; } = new List<ComplianceRule>();
        public virtual ICollection<CostRecord> CostRecords { get; set; } = new List<CostRecord>(); // Add this
        public virtual ICollection<ArbitrationDecision> ArbitrationDecisions { get; set; } = new List<ArbitrationDecision>();
    }
}
