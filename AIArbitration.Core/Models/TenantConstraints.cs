using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class TenantConstraints
    {
        public string TenantId { get; set; } = string.Empty;
        public List<string> AllowedProviders { get; set; } = new();
        public List<string> BlockedProviders { get; set; } = new();
        public List<string> AllowedModels { get; set; } = new();
        public List<string> BlockedModels { get; set; } = new();
        public decimal? MaxCostPerRequest { get; set; }
        public decimal? MonthlyBudget { get; set; }
        public int? MaxUsers { get; set; }
        public int? MaxProjects { get; set; }
        public int? MaxApiKeys { get; set; }
        public string? DefaultRegion { get; set; }
        public List<string> AllowedRegions { get; set; } = new();
        public bool RequireMfa { get; set; }
        public bool RequireDataResidency { get; set; }
        public string? ComplianceStandard { get; set; }
        public Dictionary<string, object> AdditionalConstraints { get; set; } = new();
    }
}
