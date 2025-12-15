namespace AIArbitration.Core.Models
{
    public class ComplianceCategoryScore
    {
        public string Category { get; set; } = string.Empty;
        public decimal Score { get; set; }
        public int TotalChecks { get; set; }
        public int PassedChecks { get; set; }
        public int FailedChecks { get; set; }
        public List<string> Issues { get; set; } = new();
        public int TotalRules { get; set; }
        public int CompliantRules { get; set; }
        public int NonCompliantRules { get; set; }
        public List<string> AreasForImprovement { get; set; } = new();

    }
}
