using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public class CostEstimationService : ICostEstimationService
    {
        private readonly ICostTrackingService _costTracker;
        private readonly ICandidateSelectionService _candidateSelectionService;
        private readonly ILogger<CostEstimationService> _logger;

        public CostEstimationService(
            ICostTrackingService costTracker,
            ICandidateSelectionService candidateSelectionService,
            ILogger<CostEstimationService> logger)
        {
            _costTracker = costTracker ?? throw new ArgumentNullException(nameof(costTracker));
            _candidateSelectionService = candidateSelectionService ?? throw new ArgumentNullException(nameof(candidateSelectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context)
        {
            try
            {
                var candidates = await _candidateSelectionService.GetCandidatesAsync(context);

                if (!candidates.Any())
                {
                    throw new NoSuitableModelException("No models available for cost estimation");
                }

                var tokenEstimation = await EstimateTokensAsync(request);

                var estimations = new List<CostEstimation>();
                var topCandidates = candidates
                    .OrderByDescending(c => c.FinalScore)
                    .Take(5)
                    .ToList();

                foreach (var candidate in topCandidates)
                {
                    try
                    {
                        var estimation = await _costTracker.EstimateCostAsync(
                            candidate.Model.ProviderModelId,
                            tokenEstimation.InputTokens,
                            tokenEstimation.OutputTokens);

                        estimation.ModelId = candidate.Model.ProviderModelId;
                        estimation.ModelName = candidate.Model.Name;
                        estimation.Provider = candidate.Model.Provider.Name;
                        estimation.IntelligenceScore = candidate.Model.IntelligenceScore;
                        estimation.PerformanceScore = candidate.PerformanceScore;

                        estimations.Add(estimation);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to estimate cost for model {ModelId}", candidate.Model.ProviderModelId);
                    }
                }

                if (!estimations.Any())
                {
                    throw new InvalidOperationException("Could not generate cost estimates for any model");
                }

                var aggregatedEstimation = await AggregateCostEstimations(estimations, tokenEstimation);

                _logger.LogDebug(
                    "Cost estimation completed for request {RequestId}. Range: {Min:C} - {Max:C}, Avg: {Avg:C}",
                    request.Id,
                    estimations.Min(e => e.EstimatedCost),
                    estimations.Max(e => e.EstimatedCost),
                    aggregatedEstimation.EstimatedCost);

                return aggregatedEstimation;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating cost for request {RequestId}", request.Id);
                throw;
            }
        }

        public async Task<CostEstimation> EstimateCostForModelAsync(AIModel model, ArbitrationContext context)
        {
            var inputTokens = context.EstimatedInputTokens ?? 500;
            var outputTokens = context.EstimatedOutputTokens ?? 500;

            return await _costTracker.EstimateCostAsync(
                model.ProviderModelId,
                inputTokens,
                outputTokens);
        }

        public Task<CostEstimation> AggregateCostEstimations(List<CostEstimation> estimations, TokenEstimation tokenEstimation)
        {
            var aggregated = new CostEstimation
            {
                EstimatedCost = estimations.Average(e => e.EstimatedCost),
                InputCost = estimations.Average(e => e.InputCost),
                OutputCost = estimations.Average(e => e.OutputCost),
                EstimatedInputTokens = tokenEstimation.InputTokens,
                EstimatedOutputTokens = tokenEstimation.OutputTokens,
                CostBreakdown = estimations
                    .SelectMany(e => e.CostBreakdown)
                    .GroupBy(kv => kv.Key)
                    .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value)),
                ModelRange = new CostRange
                {
                    Minimum = estimations.Min(e => e.EstimatedCost),
                    Maximum = estimations.Max(e => e.EstimatedCost),
                    Average = estimations.Average(e => e.EstimatedCost)
                }
            };

            return Task.FromResult(aggregated);
        }

        public Task<TokenEstimation> EstimateTokensAsync(ChatRequest request)
        {
            var inputTokens = EstimateTokensFromMessages(request.Messages);
            var outputTokens = request.MaxTokens;

            var estimation = new TokenEstimation
            {
                InputTokens = inputTokens,
                OutputTokens = (int)outputTokens,
                TotalTokens = inputTokens + (int)outputTokens
            };

            return Task.FromResult(estimation);
        }

        private int EstimateTokensFromMessages(List<ChatMessage> messages)
        {
            if (messages == null || !messages.Any())
                return 0;

            return (int)Math.Ceiling(messages.Sum(m => m.Content?.Length ?? 0) / 4.0);
        }
    }
}
