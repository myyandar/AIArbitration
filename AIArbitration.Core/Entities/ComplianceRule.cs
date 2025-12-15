using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class ComplianceRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Tenant reference
        public string TenantId { get; set; } = string.Empty;

        // Rule identification
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ComplianceRuleType RuleType { get; set; }
        public ComplianceStandard Standard { get; set; } // GDPR, HIPAA, SOC2, etc.
        public string? ReferenceCode { get; set; } // e.g., "GDPR-Art-5-1-c"

        // Rule configuration
        public string Condition { get; set; } = string.Empty; // JSON or expression
        public string Action { get; set; } = string.Empty; // "allow", "deny", "log", "notify", "encrypt", "anonymize"
        public string? ActionParameters { get; set; } // JSON parameters for the action
        public RulePriority Priority { get; set; } = RulePriority.Medium;

        // Scope
        public ComplianceRuleScope Scope { get; set; } = ComplianceRuleScope.Global;
        public string[]? AppliedToResources { get; set; } // JSON array
        public string[]? AppliedToUsers { get; set; } // JSON array
        public string[]? AppliedToRegions { get; set; } // JSON array

        // Validation
        public ComplianceValidationType ValidationType { get; set; }
        public string? ValidationLogic { get; set; } // Custom validation logic
        public string? ErrorMessage { get; set; }

        // Enforcement
        public bool IsEnabled { get; set; } = true;
        public bool IsMandatory { get; set; } // Cannot be disabled
        public EnforcementSeverity EnforcementSeverity { get; set; } = EnforcementSeverity.Medium;
        public string[]? NotificationChannels { get; set; } // JSON array

        // Monitoring
        public int ViolationCount { get; set; }
        public int ComplianceCheckCount { get; set; }
        public decimal ComplianceRate { get; set; } // Percentage
        public DateTime? LastViolationAt { get; set; }
        public DateTime? LastComplianceCheckAt { get; set; }

        // Documentation
        public string? DocumentationUrl { get; set; }
        public string? RemediationSteps { get; set; }
        public string? LegalReference { get; set; }

        // Metadata
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ICollection<ComplianceLog> ComplianceLogs { get; set; } = new List<ComplianceLog>();

        // Helper methods
        public bool EvaluateCondition(object context)
        {
            // Implementation would depend on your rule engine
            // Could use JSONLogic, C# expressions, or custom evaluator
            return true; // Placeholder
        }

        public ComplianceCheckResult CheckCompliance(object data)
        {
            try
            {
                var isCompliant = EvaluateCondition(data);

                return new ComplianceCheckResult
                {
                    IsCompliant = isCompliant,
                    RuleId = Id,
                    RuleName = Name,
                    Timestamp = DateTime.UtcNow,
                    Details = isCompliant ? "Rule satisfied" : ErrorMessage
                };
            }
            catch (Exception ex)
            {
                return new ComplianceCheckResult
                {
                    IsCompliant = false,
                    RuleId = Id,
                    RuleName = Name,
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message,
                    Details = "Error evaluating compliance rule"
                };
            }
        }
    }
}