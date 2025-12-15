
namespace AIArbitration.Core.Entities
{
    // Compliance check result
    public class ComplianceCheckResult
    {
        public bool IsCompliant { get; set; }
        public string RuleId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string? Error { get; set; }
        public string? Details { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
        public IEnumerable<string> Violations { get; set; }
    }
}