using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ArbitrationCandidate
    {
        public required AIModel Model { get; set; }
        public decimal TotalCost { get; set; }
        public decimal PerformanceScore { get; set; }
        public decimal ComplianceScore { get; set; }
        public decimal ValueScore { get; set; }
        public decimal FinalScore { get; set; }

        // Provider-specific info
        public string ProviderEndpoint { get; set; } = string.Empty;
        public TimeSpan EstimatedLatency { get; set; }
        public decimal ReliabilityScore { get; set; }

        // Health and availability
        public ProviderHealthStatus ProviderHealth { get; set; }
        public bool IsAvailable { get; set; } = true;
        public string? UnavailabilityReason { get; set; }

        // Cost details
        public CostEstimation CostEstimation { get; set; } = new();

        // Performance predictions
        public PerformancePrediction PerformancePrediction { get; set; } = new();

        // Compliance details
        public Dictionary<string, bool> ComplianceChecks { get; set; } = new();
        public List<string> ComplianceWarnings { get; set; } = new();

        // Decision factors
        public Dictionary<string, decimal> DecisionFactors { get; set; } = new();
        public ProviderHealthStatus ProviderHealthStatus { get; set; }

        // Helper methods
        public bool IsWithinBudget(decimal maxBudget) => TotalCost <= maxBudget;
        public bool MeetsComplianceThreshold(decimal minScore = 70) => ComplianceScore >= minScore;
        public bool MeetsPerformanceThreshold(decimal minScore = 50) => PerformanceScore >= minScore;

        public decimal GetCostPerIntelligencePoint()
        {
            return Model.IntelligenceScore > 0 ? TotalCost / Model.IntelligenceScore : decimal.MaxValue;
        }
    }
}
