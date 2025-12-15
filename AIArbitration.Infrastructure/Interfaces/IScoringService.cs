using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IScoringService
    {
        Task<decimal> CalculatePerformanceScoreAsync(AIModel model);
        Task<decimal> CalculateCostScoreAsync(AIModel model, ArbitrationContext context);
        Task<decimal> CalculateComplianceScoreAsync(AIModel model, ArbitrationContext context);
        Task<decimal> CalculateReliabilityScoreAsync(AIModel model);
        Task<decimal> CalculateExpectedCostAsync(AIModel model, ArbitrationContext context);
        Task<decimal> EstimateLatencyAsync(AIModel model);
        (decimal PerformanceWeight, decimal CostWeight, decimal ComplianceWeight, decimal ReliabilityWeight) GetScoringWeights(ArbitrationContext context);
        Task<(int Input, int Output)> GetAverageTokenUsageAsync(string taskType);
        decimal CalculateLatencyScore(double latencyMs);
        decimal CalculateThroughputScore(double tokensPerSecond);
    }
}
