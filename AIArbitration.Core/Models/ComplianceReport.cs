using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class ComplianceReport
    {
        public string TenantId { get; set; } = string.Empty;
        public DateTime ReportPeriodStart { get; set; }
        public DateTime ReportPeriodEnd { get; set; }
        public ComplianceStandard PrimaryStandard { get; set; }
        public decimal OverallComplianceScore { get; set; } // 0-100
        public List<ComplianceCategoryScore> CategoryScores { get; set; } = new();
        public List<ComplianceViolation> Violations { get; set; } = new();
        public List<ComplianceImprovement> Improvements { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
