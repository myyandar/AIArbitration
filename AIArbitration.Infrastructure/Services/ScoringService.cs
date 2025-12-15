using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public class ScoringService : IScoringService
    {
        private readonly IModelRepository _modelRepository;
        private readonly ILogger<ScoringService> _logger;

        public ScoringService(
            IModelRepository modelRepository,
            ILogger<ScoringService> logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<decimal> CalculatePerformanceScoreAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any())
            {
                _logger.LogDebug("No performance metrics found for model {ModelId}, using default score", model.ProviderModelId);
                return 50m; // Default score
            }

            var latencyScore = CalculateLatencyScore(metrics.Average(m => m.Latency.TotalMilliseconds));
            var successRateScore = metrics.Average(m => m.SuccessRate) * 100;
            var throughputScore = CalculateThroughputScore(metrics.Average(m => m.TokensPerSecond));

            return (latencyScore * 0.4m) + (successRateScore * 0.4m) + (throughputScore * 0.2m);
        }

        public async Task<decimal> CalculateCostScoreAsync(AIModel model, ArbitrationContext context)
        {
            var expectedCost = await CalculateExpectedCostAsync(model, context);

            // Lower cost = higher score (inverted)
            if (expectedCost <= 0)
                return 100m;

            // Normalize cost (assuming $10 is max expected cost per request)
            var normalizedCost = Math.Min(expectedCost / 10m, 1m);
            return 100m * (1m - normalizedCost);
        }

        public Task<decimal> CalculateComplianceScoreAsync(AIModel model, ArbitrationContext context)
        {
            if (!context.RequireDataResidency && !context.RequireEncryptionAtRest)
                return Task.FromResult(100m);

            decimal score = 100m;

            if (context.RequireDataResidency &&
                model.DataResidencyRegions?.Contains(context.RequiredRegion) != true)
                score -= 40m;

            if (context.RequireEncryptionAtRest && !model.SupportsEncryptionAtRest)
                score -= 30m;

            return Task.FromResult(Math.Max(0, score));
        }

        public async Task<decimal> CalculateReliabilityScoreAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any())
            {
                _logger.LogDebug("No reliability metrics found for model {ModelId}, using default score", model.ProviderModelId);
                return 95m; // Default reliability score
            }

            var recentMetrics = metrics
                .Where(m => m.UpdatedAt > DateTime.UtcNow.AddDays(-7))
                .ToList();

            if (!recentMetrics.Any())
                return metrics.Average(m => m.SuccessRate) * 100;

            return recentMetrics.Average(m => m.SuccessRate) * 100;
        }

        public async Task<decimal> CalculateExpectedCostAsync(AIModel model, ArbitrationContext context)
        {
            var avgTokens = await GetAverageTokenUsageAsync(context.TaskType);
            var inputTokens = context.EstimatedInputTokens ?? avgTokens.Input;
            var outputTokens = context.EstimatedOutputTokens ?? avgTokens.Output;

            var inputCost = (inputTokens / 1_000_000m) * model.CostPerMillionInputTokens;
            var outputCost = (outputTokens / 1_000_000m) * model.CostPerMillionOutputTokens;

            return inputCost + outputCost;
        }

        public async Task<decimal> EstimateLatencyAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any())
            {
                _logger.LogDebug("No latency metrics found for model {ModelId}, using default latency", model.ProviderModelId);
                return 1000m; // Default latency in ms
            }

            return (decimal)metrics.Average(m => m.Latency.Milliseconds);
        }

        public (decimal PerformanceWeight, decimal CostWeight, decimal ComplianceWeight, decimal ReliabilityWeight)
            GetScoringWeights(ArbitrationContext context)
        {
            return context.TaskType?.ToLower() switch
            {
                "cost_sensitive" => (0.3m, 0.5m, 0.1m, 0.1m),
                "performance_critical" => (0.6m, 0.1m, 0.2m, 0.1m),
                "latency_sensitive" => (0.5m, 0.2m, 0.1m, 0.2m),
                "reliability_focused" => (0.2m, 0.2m, 0.2m, 0.4m),
                "compliance_sensitive" => (0.2m, 0.2m, 0.5m, 0.1m),
                _ => (0.4m, 0.3m, 0.2m, 0.1m) // Default balanced weights
            };
        }

        public Task<(int Input, int Output)> GetAverageTokenUsageAsync(string taskType)
        {
            // Default token estimates based on task type
            var result = taskType?.ToLower() switch
            {
                "summarization" => (1000, 200),
                "translation" => (500, 500),
                "code_generation" => (200, 1000),
                "analysis" => (1500, 500),
                "chat" => (300, 300),
                _ => (500, 500) // Default
            };

            return Task.FromResult(result);
        }

        public decimal CalculateLatencyScore(double latencyMs)
        {
            // Lower latency = higher score
            if (latencyMs <= 100) return 100m;
            if (latencyMs <= 500) return 80m;
            if (latencyMs <= 1000) return 60m;
            if (latencyMs <= 2000) return 40m;
            if (latencyMs <= 5000) return 20m;
            return 10m;
        }

        public decimal CalculateThroughputScore(double tokensPerSecond)
        {
            // Higher throughput = higher score
            if (tokensPerSecond >= 1000) return 100m;
            if (tokensPerSecond >= 500) return 80m;
            if (tokensPerSecond >= 200) return 60m;
            if (tokensPerSecond >= 100) return 40m;
            if (tokensPerSecond >= 50) return 20m;
            return 10m;
        }
    }
}
