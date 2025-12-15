using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public class CandidateSelectionService : ICandidateSelectionService
    {
        private readonly IModelRepository _modelRepository;
        private readonly IComplianceService _complianceService;
        private readonly IUserService _userService;
        private readonly IScoringService _scoringService;
        private readonly ILogger<CandidateSelectionService> _logger;

        public CandidateSelectionService(
            IModelRepository modelRepository,
            IComplianceService complianceService,
            IUserService userService,
            IScoringService scoringService,
            ILogger<CandidateSelectionService> logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _scoringService = scoringService ?? throw new ArgumentNullException(nameof(scoringService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context)
        {
            try
            {
                var allModels = await _modelRepository.GetActiveModelsAsync();

                if (!allModels.Any())
                {
                    _logger.LogWarning("No active models found in the system");
                    return new List<ArbitrationCandidate>();
                }

                var candidates = new List<ArbitrationCandidate>();

                foreach (var model in allModels)
                {
                    try
                    {
                        if (!await IsModelEligibleAsync(model, context))
                            continue;

                        var candidate = await CreateCandidateAsync(model, context);
                        candidates.Add(candidate);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to evaluate model {ModelId} for arbitration", model.ProviderModelId);
                    }
                }

                _logger.LogDebug("Found {CandidateCount} eligible models out of {TotalModels} for context",
                    candidates.Count, allModels.Count);

                return candidates;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting arbitration candidates for context");
                throw;
            }
        }

        public async Task<bool> IsModelEligibleAsync(AIModel model, ArbitrationContext context)
        {
            // 1. Check basic criteria
            if (context.MinIntelligenceScore.HasValue &&
                model.IntelligenceScore < context.MinIntelligenceScore.Value)
                return false;

            if (context.MinContextLength.HasValue &&
                model.MaxTokens < context.MinContextLength.Value)
                return false;

            // 2. Check user/tenant restrictions
            var userConstraints = await _userService.GetUserConstraintsAsync(context.UserId);
            if (userConstraints.BlockedModels?.Contains(model.ProviderModelId) == true)
                return false;

            // 3. Check allowed/blocked lists from context
            if (context.BlockedModels?.Contains(model.ProviderModelId) == true)
                return false;

            if (context.AllowedModels?.Any() == true &&
                !context.AllowedModels.Contains(model.ProviderModelId))
                return false;

            if (context.BlockedProviders?.Contains(model.Provider.Name) == true)
                return false;

            if (context.AllowedProviders?.Any() == true &&
                !context.AllowedProviders.Contains(model.Provider.Name))
                return false;

            // 4. Check provider health
            var providerHealth = await _modelRepository.GetProviderHealthAsync(model.ProviderId);
            if (providerHealth != ProviderHealthStatus.Healthy)
                return false;

            // 5. Check compliance
            var complianceCheck = await _complianceService.CheckModelComplianceAsync(model, context);
            if (!complianceCheck.IsCompliant)
                return false;

            // 6. Check required capabilities
            if (context.RequiredCapabilities?.Any() == true)
            {
                foreach (var requirement in context.RequiredCapabilities)
                {
                    var capability = model.Capabilities?.FirstOrDefault(c => c.CapabilityType == requirement);
                    if (capability == null || capability.Score < (int)requirement)
                        return false;
                }
            }

            return true;
        }

        public async Task<ArbitrationCandidate> CreateCandidateAsync(AIModel model, ArbitrationContext context)
        {
            // Calculate scores in parallel for performance
            var performanceScoreTask = _scoringService.CalculatePerformanceScoreAsync(model);
            var costScoreTask = _scoringService.CalculateCostScoreAsync(model, context);
            var complianceScoreTask = _scoringService.CalculateComplianceScoreAsync(model, context);
            var reliabilityScoreTask = _scoringService.CalculateReliabilityScoreAsync(model);
            var latencyEstimationTask = _scoringService.EstimateLatencyAsync(model);
            var expectedCostTask = _scoringService.CalculateExpectedCostAsync(model, context);

            await Task.WhenAll(
                performanceScoreTask,
                costScoreTask,
                complianceScoreTask,
                reliabilityScoreTask,
                latencyEstimationTask,
                expectedCostTask);

            // Calculate weighted final score based on context
            var weights = _scoringService.GetScoringWeights(context);
            var finalScore = (performanceScoreTask.Result * weights.PerformanceWeight) +
                            (costScoreTask.Result * weights.CostWeight) +
                            (complianceScoreTask.Result * weights.ComplianceWeight) +
                            (reliabilityScoreTask.Result * weights.ReliabilityWeight);

            return new ArbitrationCandidate
            {
                Model = model,
                TotalCost = await expectedCostTask,
                PerformanceScore = await performanceScoreTask,
                ComplianceScore = await complianceScoreTask,
                ReliabilityScore = await reliabilityScoreTask,
                ValueScore = model.IntelligenceScore / Math.Max(await expectedCostTask, 0.001m),
                FinalScore = finalScore,
                ProviderEndpoint = model.Provider.BaseUrl,
                EstimatedLatency = TimeSpan.FromMilliseconds((double)await latencyEstimationTask),
                ProviderHealthStatus = await _modelRepository.GetProviderHealthAsync(model.ProviderId)
            };
        }

        public async Task<List<ArbitrationCandidate>> ScoreAndRankCandidatesAsync(
            List<ArbitrationCandidate> candidates,
            ArbitrationContext context)
        {
            var scoredCandidates = new List<ArbitrationCandidate>();

            foreach (var candidate in candidates)
            {
                try
                {
                    // Recalculate scores with context-specific weights
                    var weights = _scoringService.GetScoringWeights(context);

                    var finalScore = (candidate.PerformanceScore * weights.PerformanceWeight) +
                                    (candidate.ComplianceScore * weights.ComplianceWeight) +
                                    (candidate.ReliabilityScore * weights.ReliabilityWeight);

                    // Apply cost scoring (inverse: lower cost = higher score)
                    var costScore = await _scoringService.CalculateCostScoreAsync(candidate.Model, context);
                    finalScore += costScore * weights.CostWeight;

                    candidate.FinalScore = finalScore;
                    candidate.ValueScore = candidate.Model.IntelligenceScore / Math.Max(candidate.TotalCost, 0.001m);

                    scoredCandidates.Add(candidate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to score candidate {ModelId}", candidate.Model.ProviderModelId);
                }
            }

            return scoredCandidates
                .OrderByDescending(c => c.FinalScore)
                .ThenByDescending(c => c.ValueScore)
                .ToList();
        }

        public List<ArbitrationCandidate> ApplyBusinessRules(
            List<ArbitrationCandidate> candidates,
            ArbitrationContext context)
        {
            var filteredCandidates = candidates
                .Where(c => c.FinalScore >= 50) // Minimum score threshold
                .Where(c => context.MaxLatency == null || c.EstimatedLatency <= context.MaxLatency)
                .Where(c => !context.MaxCost.HasValue || c.TotalCost <= context.MaxCost.Value)
                .ToList();

            // If no candidates meet strict criteria, return top candidates
            if (!filteredCandidates.Any())
            {
                return candidates
                    .OrderByDescending(c => c.FinalScore)
                    .Take(3)
                    .ToList();
            }

            return filteredCandidates;
        }

        public ArbitrationCandidate SelectBestModel(List<ArbitrationCandidate> candidates, ArbitrationContext context)
        {
            if (!candidates.Any())
                throw new NoSuitableModelException("No candidates available for selection");

            return context.SelectionStrategy?.ToLower() switch
            {
                "cost_optimized" => candidates.OrderBy(c => c.TotalCost).First(),
                "performance_critical" => candidates.OrderByDescending(c => c.PerformanceScore).First(),
                "latency_sensitive" => candidates.OrderBy(c => c.EstimatedLatency).First(),
                "reliability_focused" => candidates.OrderByDescending(c => c.ReliabilityScore).First(),
                "balanced" => candidates.OrderByDescending(c => c.FinalScore).First(),
                _ => candidates.OrderByDescending(c => c.FinalScore).First()
            };
        }

        public List<ArbitrationCandidate> PrepareFallbackCandidates(
            List<ArbitrationCandidate> candidates,
            ArbitrationCandidate selectedModel)
        {
            return candidates
                .Where(c => c.Model.Id != selectedModel.Model.Id)
                .OrderByDescending(c => c.FinalScore)
                .Take(3)
                .ToList();
        }
    }
}
