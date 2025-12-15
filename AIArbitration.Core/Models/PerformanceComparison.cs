using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class PerformanceComparison
    {
        public List<string> ModelIds { get; set; } = new();
        public ArbitrationContext Context { get; set; } = new();
        public Dictionary<string, PerformancePrediction> Predictions { get; set; } = new();
        public string? RecommendedModel { get; set; }
        public Dictionary<string, decimal> ComparisonScores { get; set; } = new();
        public Dictionary<string, string> Strengths { get; set; } = new();
        public Dictionary<string, string> Weaknesses { get; set; } = new();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
