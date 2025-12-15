using AIArbitration.Core.Models;
using AIArbitration.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ArbitrationResult
    {
        public required ArbitrationCandidate SelectedModel { get; set; }
        public required List<ArbitrationCandidate> AllCandidates { get; set; }
        public required List<ArbitrationCandidate> FallbackCandidates { get; set; }
        public required CostEstimation EstimatedCost { get; set; }
        public required PerformancePrediction PerformancePrediction { get; set; }
        public required DateTime Timestamp { get; set; }
        public required string DecisionId { get; set; }

        // Decision metadata
        public Dictionary<string, object> DecisionFactors { get; set; } = new();
        public List<string> ExcludedModels { get; set; } = new();
        public string SelectionStrategy { get; set; } = string.Empty;
        public TimeSpan SelectionDuration { get; set; }

        // Context
        public ArbitrationContext? Context { get; set; }

        // Performance metrics
        public decimal SelectionConfidence { get; set; } = 1.0m;
        public bool IsOptimalChoice { get; set; } = true;
        public string? OptimizationGoal { get; set; }

        // Audit trail
        public string? AuditLogId { get; set; }
        public Dictionary<string, object>? AuditData { get; set; }

        // Helper methods
        public bool HasFallbacks => FallbackCandidates.Any();
        public int TotalCandidatesConsidered => AllCandidates.Count;

        public decimal GetCostSavingsComparedTo(ArbitrationCandidate alternative)
        {
            return alternative.TotalCost - SelectedModel.TotalCost;
        }

        public decimal GetPerformanceImprovementComparedTo(ArbitrationCandidate alternative)
        {
            return SelectedModel.PerformanceScore - alternative.PerformanceScore;
        }
    }
}
