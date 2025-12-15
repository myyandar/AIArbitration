using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class UpdateComplianceRuleCommand
    {
        public string RuleId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplianceStandard Standard { get; set; }
        public ComplianceRuleType RuleType { get; set; }
    }
}
