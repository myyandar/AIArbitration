using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text.Json;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Core.Models;
using AIArbitration.Core;

namespace AIArbitration.Infrastructure.Services
{
    public class ArbitrationEngine : IArbitrationEngine
    {
        private readonly IModelRepository _modelRepository;
        private readonly IProviderAdapterFactory _adapterFactory;
        private readonly ICostTrackingService _costTracker;
        private readonly IPerformancePredictor _performancePredictor;
        private readonly IUserService _userService;
        private readonly ITenantService _tenantService;
        private readonly IBudgetService _budgetService;
        private readonly IComplianceService _complianceService;
        private readonly ILogger<ArbitrationEngine> _logger;
        private readonly IRateLimiter _rateLimiter;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly AIArbitrationDbContext _databaseContext;

        public ArbitrationEngine(
            IModelRepository modelRepository,
            IProviderAdapterFactory adapterFactory,
            ICostTrackingService costTracker,
            IPerformancePredictor performancePredictor,
            IUserService userService,
            ITenantService tenantService,
            IBudgetService budgetService,
            IComplianceService complianceService,
            ILogger<ArbitrationEngine> logger,
            IRateLimiter rateLimiter,
            ICircuitBreaker circuitBreaker,
            AIArbitrationDbContext databaseContext)
        {
            _modelRepository = modelRepository;
            _adapterFactory = adapterFactory;
            _costTracker = costTracker;
            _performancePredictor = performancePredictor;
            _userService = userService;
            _tenantService = tenantService;
            _budgetService = budgetService;
            _complianceService = complianceService;
            _logger = logger;
            _rateLimiter = rateLimiter;
            _circuitBreaker = circuitBreaker;
            _databaseContext = databaseContext;
        }

        public async Task<ArbitrationResult> SelectModelAsync(ArbitrationContext context)
        {
            var decisionId = Guid.NewGuid().ToString();
            _logger.LogInformation("Starting model selection {DecisionId} for user {UserId}", decisionId, context.UserId);

            try
            {
                // 1. Check rate limits
                await _rateLimiter.CheckRateLimitAsync(context);

                // 2. Get all eligible models
                var candidates = await GetCandidatesAsync(context);

                if (!candidates.Any())
                    throw new InvalidOperationException("No suitable models found");

                // 3. Score and rank candidates
                var scoredCandidates = await ScoreAndRankCandidatesAsync(candidates, context);

                // 4. Select best model
                var selectedModel = SelectBestModel(scoredCandidates, context);

                // 5. Get performance prediction
                var performance = await PredictPerformanceAsync(context);

                // 6. Estimate cost
                var costEstimation = await EstimateCostForModelAsync(selectedModel.Model, context);

                // 7. Prepare fallback candidates
                var fallbackCandidates = scoredCandidates
                    .Where(c => c.Model.Id != selectedModel.Model.Id)
                    .OrderByDescending(c => c.FinalScore)
                    .Take(3)
                    .ToList();

                // 8. Record decision
                await RecordArbitrationDecisionAsync(decisionId, context, selectedModel, scoredCandidates);

                return new ArbitrationResult
                {
                    SelectedModel = selectedModel,
                    AllCandidates = scoredCandidates,
                    FallbackCandidates = fallbackCandidates,
                    EstimatedCost = costEstimation,
                    PerformancePrediction = performance,
                    Timestamp = DateTime.UtcNow,
                    DecisionId = decisionId,
                    DecisionFactors = new Dictionary<string, object>
                    {
                        ["primary_factor"] = context.TaskType,
                        ["budget_constrained"] = context.MaxCost.HasValue,
                        ["compliance_requirements"] = context.RequireDataResidency || context.RequireEncryptionAtRest,
                        ["candidate_count"] = scoredCandidates.Count
                    },
                    ExcludedModels = scoredCandidates
                        .Where(c => c.FinalScore < 50)
                        .Select(c => c.Model.ProviderModelId)
                        .ToList(),
                    SelectionStrategy = DetermineSelectionStrategy(context)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model selection failed {DecisionId}", decisionId);
                throw;
            }
        }

        public async Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context)
        {
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // 1. Select model
                var arbitrationResult = await SelectModelAsync(context);
                var selectedModel = arbitrationResult.SelectedModel;

                // 2. Get adapter for selected provider
                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

                // 3. Execute with circuit breaker
                var response = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    var result = await adapter.SendChatCompletionAsync(request);
                        if (result != null)
                            request.ModelId = selectedModel.Model.ProviderModelId;

                    return result;
                }, selectedModel.Model.Provider.Name);

                stopwatch.Stop();

                var model = Guid.TryParse(selectedModel.Model.Id, out Guid guidvalue);
       
                // 4. Update model performance metrics
                await UpdateModelPerformanceAsync(guidvalue, response.ProcessingTime, true);

                // 5. Record usage for billing
                await _costTracker.RecordUsageAsync(new UsageRecord
                {
                    TenantId = context.TenantId,
                    UserId = context.UserId,
                    ProjectId = context.ProjectId,
                    ModelId = selectedModel.Model.ProviderModelId,
                    Provider = selectedModel.Model.Provider.Name,
                    InputTokens = response.InputTokens,
                    OutputTokens = response.OutputTokens,
                    Cost = response.Cost,
                    ProcessingTime = response.ProcessingTime,
                    Timestamp = DateTime.UtcNow,
                    RequestId = response.Id,
                    Success = true
                });

                // 6. Check for budget warnings
                await CheckBudgetWarningsAsync(context, response.Cost);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Record failure
                await RecordFailedRequestAsync(context, ex, stopwatch.Elapsed);

                // Try fallback if available
                return await TryFallbackExecutionAsync(request, context, ex);
            }
        }

        public async Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context)
        {
            var models = await _modelRepository.GetActiveModelsAsync();
            var candidates = new List<ArbitrationCandidate>();

            foreach (var model in models)
            {
                try
                {
                    // Check basic eligibility
                    if (!await IsModelEligibleAsync(model, context))
                        continue;

                    var candidate = await CreateCandidateAsync(model, context);
                    candidates.Add(candidate);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to evaluate model {ModelId}", model.ProviderModelId);
                }
            }

            return candidates;
        }

        public async Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context)
        {
            var candidates = await GetCandidatesAsync(context);

            if (!candidates.Any())
                throw new InvalidOperationException("No models available for cost estimation");

            // Estimate tokens based on request
            var tokenEstimation = await EstimateTokensAsync(request);

            var estimations = new List<CostEstimation>();

            foreach (var candidate in candidates.Take(5)) // Limit to top 5
            {
                var estimation = new CostEstimation
                {
                    EstimatedInputTokens = tokenEstimation.InputTokens,
                    EstimatedOutputTokens = tokenEstimation.OutputTokens,
                    InputCost = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens,
                    OutputCost = (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
                    EstimatedCost = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens +
                                  (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
                    CostBreakdown = new Dictionary<string, decimal>
                    {
                        ["input_cost"] = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens,
                        ["output_cost"] = (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
                        ["model_license"] = candidate.Model.Tier == ModelTier.Premium ? 0.001m : 0m,
                        ["infrastructure"] = 0.0005m
                    }
                };

                estimations.Add(estimation);
            }

            // Return the average estimation
            return new CostEstimation
            {
                EstimatedCost = estimations.Average(e => e.EstimatedCost),
                InputCost = estimations.Average(e => e.InputCost),
                OutputCost = estimations.Average(e => e.OutputCost),
                EstimatedInputTokens = tokenEstimation.InputTokens,
                EstimatedOutputTokens = tokenEstimation.OutputTokens,
                CostBreakdown = estimations
                    .SelectMany(e => e.CostBreakdown)
                    .GroupBy(kv => kv.Key)
                    .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value))
            };
        }

        public async Task<PerformancePrediction> PredictPerformanceAsync(ArbitrationContext context)
        {
            var candidates = await GetCandidatesAsync(context);

            if (!candidates.Any())
                throw new InvalidOperationException("No models available for performance prediction");

            var predictions = new List<PerformancePrediction>();

            foreach (var candidate in candidates.Take(3))
            {
                var prediction = await _performancePredictor.PredictAsync(candidate.Model, context);
                predictions.Add(prediction);
            }

            // Return best prediction
            return predictions.OrderByDescending(p => p.ReliabilityScore).First();
        }

        #region Missing Method Implementations (Helper Methods)

        private async Task<List<ArbitrationCandidate>> ScoreAndRankCandidatesAsync(List<ArbitrationCandidate> candidates, ArbitrationContext context)
        {
            var scoredCandidates = new List<ArbitrationCandidate>();

            foreach (var candidate in candidates)
            {
                try
                {
                    // Calculate additional scores
                    var performanceScore = await CalculatePerformanceScoreAsync(candidate.Model);
                    var costScore = await CalculateCostScoreAsync(candidate.Model, context);
                    var complianceScore = await CalculateComplianceScoreAsync(candidate.Model, context);
                    var reliabilityScore = await CalculateReliabilityScoreAsync(candidate.Model);

                    // Apply weights based on context
                    decimal performanceWeight = 0.4m;
                    decimal costWeight = 0.3m;
                    decimal complianceWeight = 0.2m;
                    decimal reliabilityWeight = 0.1m;

                    if (context.TaskType == "cost_sensitive")
                    {
                        costWeight = 0.5m;
                        performanceWeight = 0.3m;
                    }
                    else if (context.TaskType == "performance_critical")
                    {
                        performanceWeight = 0.6m;
                        costWeight = 0.2m;
                    }

                    var finalScore = (performanceScore * performanceWeight) +
                                    (costScore * costWeight) +
                                    (complianceScore * complianceWeight) +
                                    (reliabilityScore * reliabilityWeight);

                    // Update candidate with calculated scores
                    candidate.PerformanceScore = performanceScore;
                    candidate.ComplianceScore = complianceScore;
                    candidate.ReliabilityScore = reliabilityScore;
                    candidate.FinalScore = finalScore;
                    candidate.EstimatedLatency = await EstimateLatencyAsync(candidate.Model);
                    candidate.TotalCost = await CalculateExpectedCostAsync(candidate.Model, context);
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

        private ArbitrationCandidate SelectBestModel(List<ArbitrationCandidate> candidates, ArbitrationContext context)
        {
            if (!candidates.Any())
                throw new InvalidOperationException("No candidates available for selection");

            // Filter out models that don't meet minimum requirements
            var eligibleCandidates = candidates.Where(c =>
                c.FinalScore >= 50 && // Minimum score threshold
                (context.MaxLatency == null || c.EstimatedLatency.TotalMilliseconds <= context.MaxLatency.Value.TotalMilliseconds) &&
                (!context.MaxCost.HasValue || c.TotalCost <= context.MaxCost.Value))
                .ToList();

            if (!eligibleCandidates.Any())
            {
                // If no candidates meet strict criteria, relax the constraints
                eligibleCandidates = candidates
                    .OrderByDescending(c => c.FinalScore)
                    .Take(3)
                    .ToList();
            }

            // Selection strategy
            return context.TaskType switch
            {
                "cost_optimized" => eligibleCandidates.OrderBy(c => c.TotalCost).First(),
                "performance_critical" => eligibleCandidates.OrderByDescending(c => c.PerformanceScore).First(),
                "balanced" => eligibleCandidates.OrderByDescending(c => c.FinalScore).First(),
                _ => eligibleCandidates.OrderByDescending(c => c.FinalScore).First()
            };
        }

        private async Task UpdateModelPerformanceAsync(Guid modelId, TimeSpan processingTime, bool success)
        {
            try
            {
                await _modelRepository.RecordModelPerformanceAsync(new ModelPerformanceMetric
                {
                    ModelId = modelId,
                    LatencyMs = processingTime.TotalMilliseconds,
                    SuccessRate = success ? 1.0 : 0.0,
                    TokensPerSecond = 0, // Would need actual token count
                    Timestamp = DateTime.UtcNow,
                    ErrorRate = success ? 0.0 : 1.0
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update performance metrics for model {ModelId}", modelId);
            }
        }

        private async Task CheckBudgetWarningsAsync(ArbitrationContext context, decimal cost)
        {
            try
            {
                if (context.TenantId != Guid.Empty.ToString())
                {
                    var budgetStatus = await _budgetService.GetBudgetStatusAsync(context.TenantId);

                    if (budgetStatus.UtilizationPercentage > 80)
                    {
                        _logger.LogWarning("Budget utilization at {Percentage}% for tenant {TenantId}",
                            budgetStatus.UtilizationPercentage, context.TenantId);
                    }

                    if (budgetStatus.RemainingBudget < 100 && budgetStatus.RemainingBudget > 0)
                    {
                        _logger.LogWarning("Low budget remaining: {Remaining} for tenant {TenantId}",
                            budgetStatus.RemainingBudget, context.TenantId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check budget warnings for tenant {TenantId}", context.TenantId);
            }
        }

        private async Task RecordFailedRequestAsync(ArbitrationContext context, Exception exception, TimeSpan elapsed)
        {
            try
            {
                var failureRecord = new RequestFailure
                {
                    TenantId = Guid.Parse(context.TenantId),
                    UserId = Guid.Parse(context.UserId),
                    ProjectId = Guid.Parse(context.ProjectId),
                    TaskType = context.TaskType,
                    ErrorMessage = exception.Message,
                    ErrorType = exception.GetType().Name,
                    Duration = elapsed,
                    Timestamp = DateTime.UtcNow,
                    ContextData = JsonSerializer.Serialize(new
                    {
                        context.MinIntelligenceScore,
                        context.MaxCost,
                        context.RequiredRegion
                    })
                };

                await _modelRepository.RecordRequestFailureAsync(failureRecord);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record request failure");
            }
        }

        private async Task<ModelResponse> TryFallbackExecutionAsync(ChatRequest request, ArbitrationContext context, Exception originalException)
        {
            _logger.LogInformation("Attempting fallback execution after failure: {Error}", originalException.Message);

            try
            {
                // Get arbitration result to access fallback candidates
                var arbitrationResult = await SelectModelAsync(context);
                var fallbackCandidates = arbitrationResult.FallbackCandidates;

                foreach (var fallback in fallbackCandidates)
                {
                    try
                    {
                        _logger.LogInformation("Trying fallback model: {ModelId}", fallback.Model.ProviderModelId);

                        var adapter = await _adapterFactory.GetAdapterForModelAsync(fallback.Model.ProviderModelId);
                        var response = await adapter.SendChatCompletionAsync(request with
                        {
                            ModelId = fallback.Model.ProviderModelId
                        });

                        _logger.LogInformation("Fallback successful with model: {ModelId}", fallback.Model.ProviderModelId);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fallback model {ModelId} also failed", fallback.Model.ProviderModelId);
                    }
                }

                throw new InvalidOperationException("All models failed, including fallbacks", originalException);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All fallback attempts failed");
                throw;
            }
        }

        private async Task<TokenEstimation> EstimateTokensAsync(ChatRequest request)
        {
            // Simple estimation based on content length
            // In production, use a proper tokenizer
            var inputTokens = EstimateTokensFromText(request.Messages.Sum(m => m.Content?.Length ?? 0));
            var outputTokens = EstimateTokensFromText(request.MaxTokens ?? 100);

            return new TokenEstimation
            {
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens
            };
        }

        private int EstimateTokensFromText(int textLength)
        {
            // Rough estimation: ~4 characters per token for English
            return (int)Math.Ceiling(textLength / 4.0);
        }

        private async Task<decimal> CalculateComplianceScoreAsync(AIModel model, ArbitrationContext context)
        {
            if (!context.RequireDataResidency && !context.RequireEncryptionAtRest)
                return 100m;

            decimal score = 100m;

            if (context.RequireDataResidency && model.DataResidencyRegions?.Contains(context.RequiredRegion) != true)
                score -= 40m;

            if (context.RequireEncryptionAtRest && !model.SupportsEncryptionAtRest)
                score -= 30m;

            return Math.Max(0, score);
        }

        private async Task<decimal> CalculateExpectedCostAsync(AIModel model, ArbitrationContext context)
        {
            // Get average token usage for the task type
            var avgTokens = await GetAverageTokenUsageAsync(context.TaskType);

            var inputCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens;
            var outputCost = (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens;

            return inputCost + outputCost;
        }

        private async Task<TimeSpan> EstimateLatencyAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any())
                return 1000m; // Default latency in ms

            return metrics.Average(m => m.LatencyMs);
        }

        private async Task<decimal> CalculateReliabilityScoreAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any())
                return 95m; // Default reliability score

            var recentMetrics = metrics
                .Where(m => m.Timestamp > DateTime.UtcNow.AddDays(-7))
                .ToList();

            if (!recentMetrics.Any())
                return metrics.Average(m => m.SuccessRate) * 100;

            return recentMetrics.Average(m => m.SuccessRate) * 100;
        }

        private decimal CalculateLatencyScore(double latencyMs)
        {
            // Lower latency = higher score
            if (latencyMs <= 100) return 100m;
            if (latencyMs <= 500) return 80m;
            if (latencyMs <= 1000) return 60m;
            if (latencyMs <= 2000) return 40m;
            if (latencyMs <= 5000) return 20m;
            return 10m;
        }

        private decimal CalculateThroughputScore(double tokensPerSecond)
        {
            // Higher throughput = higher score
            if (tokensPerSecond >= 1000) return 100m;
            if (tokensPerSecond >= 500) return 80m;
            if (tokensPerSecond >= 200) return 60m;
            if (tokensPerSecond >= 100) return 40m;
            if (tokensPerSecond >= 50) return 20m;
            return 10m;
        }

        private async Task<(int Input, int Output)> GetAverageTokenUsageAsync(string taskType)
        {
            // Default token estimates based on task type
            return taskType switch
            {
                "summarization" => (1000, 200),
                "translation" => (500, 500),
                "code_generation" => (200, 1000),
                "analysis" => (1500, 500),
                "chat" => (300, 300),
                _ => (500, 500) // Default
            };
        }

        #endregion

        #region Existing Private Methods

        private async Task<bool> IsModelEligibleAsync(AIModel model, ArbitrationContext context)
        {
            // Check basic criteria
            if (context.MinIntelligenceScore.HasValue &&
                model.IntelligenceScore < context.MinIntelligenceScore.Value)
                return false;

            // Check user/tenant restrictions
            var userConstraints = await _userService.GetUserConstraintsAsync(context.UserId);
            if (userConstraints.BlockedModels.Contains(model.ProviderModelId))
                return false;

            // Check provider health
            var providerHealth = await _modelRepository.GetProviderHealthAsync(model.ProviderId);
            if (providerHealth?.Status != ProviderHealthStatus.Healthy)
                return false;

            // Check compliance
            var complianceCheck = await _complianceService.CheckModelComplianceAsync(model, context);
            if (!complianceCheck.IsCompliant)
                return false;

            return true;
        }

        private async Task<ArbitrationCandidate> CreateCandidateAsync(AIModel model, ArbitrationContext context)
        {
            // Calculate scores
            var performanceScore = await CalculatePerformanceScoreAsync(model);
            var costScore = await CalculateCostScoreAsync(model, context);
            var complianceScore = await CalculateComplianceScoreAsync(model, context);
            var reliabilityScore = await CalculateReliabilityScoreAsync(model);

            // Weighted final score
            var finalScore = (performanceScore * 0.4m) +
                            (costScore * 0.3m) +
                            (complianceScore * 0.2m) +
                            (reliabilityScore * 0.1m);

            return new ArbitrationCandidate
            {
                Model = model,
                TotalCost = await CalculateExpectedCostAsync(model, context),
                PerformanceScore = performanceScore,
                ComplianceScore = complianceScore,
                ReliabilityScore = reliabilityScore,
                ValueScore = model.IntelligenceScore / Math.Max(await CalculateExpectedCostAsync(model, context), 0.001m),
                FinalScore = finalScore,
                ProviderEndpoint = model.Provider.BaseUrl,
                EstimatedLatency = await EstimateLatencyAsync(model),
                ProviderHealth = await _modelRepository.GetProviderHealthAsync(model.ProviderId)
            };
        }

        private async Task<decimal> CalculatePerformanceScoreAsync(AIModel model)
        {
            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

            if (!metrics.Any()) return 50m; // Default score

            var latencyScore = CalculateLatencyScore(metrics.Average(m => m.LatencyMs));
            var successRateScore = metrics.Average(m => m.SuccessRate) * 100;
            var throughputScore = CalculateThroughputScore(metrics.Average(m => m.TokensPerSecond));

            return (latencyScore * 0.4m) + (successRateScore * 0.4m) + (throughputScore * 0.2m);
        }

        private async Task<decimal> CalculateCostScoreAsync(AIModel model, ArbitrationContext context)
        {
            var expectedCost = await CalculateExpectedCostAsync(model, context);

            // Lower cost = higher score (inverted)
            if (expectedCost <= 0) return 100m;

            // Normalize cost (assuming $10 is max expected cost per request)
            var normalizedCost = Math.Min(expectedCost / 10m, 1m);
            return 100m * (1m - normalizedCost);
        }

        private async Task<CostEstimation> EstimateCostForModelAsync(AIModel model, ArbitrationContext context)
        {
            // Get average token usage for this task type
            var avgTokens = await GetAverageTokenUsageAsync(context.TaskType);

            return new CostEstimation
            {
                EstimatedInputTokens = avgTokens.Input,
                EstimatedOutputTokens = avgTokens.Output,
                InputCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens,
                OutputCost = (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens,
                EstimatedCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens +
                              (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens
            };
        }

        private async Task RecordArbitrationDecisionAsync(
            string decisionId,
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            List<ArbitrationCandidate> allCandidates)
        {
            // Store decision in database for analytics
            var decision = new ArbitrationDecision
            {
                Id = decisionId,
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                SelectedModelId = selectedModel.Model.Id,
                TaskType = context.TaskType,
                CandidateCount = allCandidates.Count,
                SelectionDuration = 0, // Will be set by caller
                Timestamp = DateTime.UtcNow,
                DecisionFactorsJson = JsonSerializer.Serialize(new
                {
                    context.MinIntelligenceScore,
                    context.MaxCost,
                    context.RequiredRegion,
                    RequiredCapabilities = context.RequiredCapabilities.Keys.ToList()
                })
            };

            await _modelRepository.RecordArbitrationDecisionAsync(decision);
        }

        private string DetermineSelectionStrategy(ArbitrationContext context)
        {
            if (context.MaxCost.HasValue && context.MaxCost < 0.10m)
                return "cost_optimized";

            if (context.MinIntelligenceScore.HasValue && context.MinIntelligenceScore > 70)
                return "intelligence_optimized";

            if (context.MaxLatency.HasValue && context.MaxLatency < TimeSpan.FromSeconds(2))
                return "latency_optimized";

            if (context.RequiredCapabilities.Any())
                return "capability_optimized";

            return "balanced";
        }

        #endregion

        #region Missing Interface Methods

        #region Missing Interface Methods

        public async Task<StreamingModelResponse> ExecuteStreamingAsync(ChatRequest request, ArbitrationContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            StreamingModelResponse streamingResponse = null;

            try
            {
                // 1. Select model
                var arbitrationResult = await SelectModelAsync(context);
                var selectedModel = arbitrationResult.SelectedModel;

                // 2. Get adapter for selected provider
                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

                // 3. Execute streaming with circuit breaker
                streamingResponse = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    var streamResult = await adapter.SendChatCompletionStreamAsync(request with
                    {
                        ModelId = selectedModel.Model.ProviderModelId
                    });

                    return streamResult;
                }, selectedModel.Model.Provider.Name);

                stopwatch.Stop();
                streamingResponse.ProcessingTime = stopwatch.Elapsed;

                // 4. Update model performance metrics
                await UpdateModelPerformanceAsync(selectedModel.Model.Id, streamingResponse.ProcessingTime, true);

                // 5. Record usage for billing (will be done when streaming completes)
                streamingResponse.OnCompletion = async (inputTokens, outputTokens, cost) =>
                {
                    await _costTracker.RecordUsageAsync(new UsageRecord
                    {
                        TenantId = context.TenantId,
                        UserId = context.UserId,
                        ProjectId = context.ProjectId,
                        ModelId = selectedModel.Model.ProviderModelId,
                        Provider = selectedModel.Model.Provider.Name,
                        InputTokens = inputTokens,
                        OutputTokens = outputTokens,
                        Cost = cost,
                        ProcessingTime = streamingResponse.ProcessingTime,
                        Timestamp = DateTime.UtcNow,
                        RequestId = streamingResponse.Id,
                        Success = true
                    });

                    await CheckBudgetWarningsAsync(context, cost);
                };

                return streamingResponse;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                // Record failure
                await RecordFailedRequestAsync(context, ex, stopwatch.Elapsed);

                // Try fallback if available
                return await TryFallbackStreamingAsync(request, context, ex);
            }
        }

        public async Task<List<ArbitrationResult>> SelectModelsAsync(List<ArbitrationContext> contexts)
        {
            _logger.LogInformation("Starting batch model selection for {Count} contexts", contexts.Count);

            var results = new List<ArbitrationResult>();
            var tasks = new List<Task<ArbitrationResult>>();

            // Process each context in parallel (with some limits)
            var semaphore = new SemaphoreSlim(5); // Limit to 5 concurrent selections
            foreach (var context in contexts)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await SelectModelAsync(context);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }

            // Wait for all selections to complete
            var batchResults = await Task.WhenAll(tasks);
            results.AddRange(batchResults);

            // Log statistics
            var selectedModels = results.Select(r => r.SelectedModel.Model.ProviderModelId).Distinct().ToList();
            _logger.LogInformation("Batch selection completed. Unique models selected: {ModelCount}", selectedModels.Count);

            return results;
        }

        public async Task<BatchExecutionResult> ExecuteBatchAsync(List<ChatRequest> requests, ArbitrationContext context)
        {
            _logger.LogInformation("Starting batch execution of {Count} requests", requests.Count);

            var stopwatch = Stopwatch.StartNew();
            var batchResult = new BatchExecutionResult
            {
                BatchId = Guid.NewGuid().ToString(),
                StartTime = DateTime.UtcNow,
                TotalRequests = requests.Count
            };

            try
            {
                // 1. Select model for the batch (use the same model for all requests to optimize)
                var arbitrationResult = await SelectModelAsync(context);
                var selectedModel = arbitrationResult.SelectedModel;

                // 2. Get adapter for selected provider
                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

                // 3. Prepare batch execution
                var batchRequests = requests.Select(r => r with { ModelId = selectedModel.Model.ProviderModelId }).ToList();

                // 4. Execute batch with circuit breaker
                var batchResponses = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    return await adapter.SendBatchChatCompletionAsync(batchRequests);
                }, selectedModel.Model.Provider.Name);

                stopwatch.Stop();

                // 5. Process results
                var successfulResponses = new List<ModelResponse>();
                var failedResponses = new List<FailedRequest>();
                var totalCost = 0m;

                foreach (var response in batchResponses)
                {
                    if (response.Success)
                    {
                        successfulResponses.Add(response);
                        totalCost += response.Cost;

                        // Record usage
                        await _costTracker.RecordUsageAsync(new UsageRecord
                        {
                            TenantId = context.TenantId,
                            UserId = context.UserId,
                            ProjectId = context.ProjectId,
                            ModelId = selectedModel.Model.ProviderModelId,
                            Provider = selectedModel.Model.Provider.Name,
                            InputTokens = response.InputTokens,
                            OutputTokens = response.OutputTokens,
                            Cost = response.Cost,
                            ProcessingTime = response.ProcessingTime,
                            Timestamp = DateTime.UtcNow,
                            RequestId = response.Id,
                            Success = true
                        });
                    }
                    else
                    {
                        failedResponses.Add(new FailedRequest
                        {
                            Request = requests[batchResponses.IndexOf(response)],
                            ErrorMessage = response.ErrorMessage,
                            ErrorType = "BatchExecutionError"
                        });
                    }
                }

                // 6. Update model performance metrics
                var averageProcessingTime = successfulResponses.Any()
                    ? TimeSpan.FromMilliseconds(successfulResponses.Average(r => r.ProcessingTime.TotalMilliseconds))
                    : TimeSpan.Zero;

                await UpdateModelPerformanceAsync(
                    selectedModel.Model.Id,
                    averageProcessingTime,
                    failedResponses.Count == 0);

                // 7. Check budget warnings
                await CheckBudgetWarningsAsync(context, totalCost);

                // 8. Populate batch result
                batchResult.EndTime = DateTime.UtcNow;
                batchResult.TotalDuration = stopwatch.Elapsed;
                batchResult.SuccessfulCount = successfulResponses.Count;
                batchResult.FailedCount = failedResponses.Count;
                batchResult.TotalCost = totalCost;
                batchResult.AverageProcessingTime = averageProcessingTime;
                batchResult.SelectedModel = selectedModel.Model.ProviderModelId;
                batchResult.Responses = successfulResponses;
                batchResult.FailedRequests = failedResponses;
                batchResult.BatchEfficiency = successfulResponses.Count / (double)requests.Count;

                _logger.LogInformation("Batch execution completed: {Successful}/{Total} successful",
                    successfulResponses.Count, requests.Count);

                return batchResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                batchResult.EndTime = DateTime.UtcNow;
                batchResult.TotalDuration = stopwatch.Elapsed;
                batchResult.Error = ex.Message;
                batchResult.FailedCount = requests.Count;

                _logger.LogError(ex, "Batch execution failed for batch {BatchId}", batchResult.BatchId);

                throw;
            }
        }

        public async Task<ArbitrationRules> OptimizeRulesAsync(ArbitrationContext context)
        {
            _logger.LogInformation("Starting rule optimization for context {ContextType}", context.TaskType);

            try
            {
                // 1. Analyze historical decisions
                var recentDecisions = await _modelRepository.GetRecentArbitrationDecisionsAsync(
                    context.TenantId,
                    DateTime.UtcNow.AddDays(-30));

                if (!recentDecisions.Any())
                {
                    _logger.LogWarning("No historical decisions found for rule optimization");
                    return new ArbitrationRules();
                }

                // 2. Calculate success rates by model and task type
                var decisionsByTaskType = recentDecisions
                    .GroupBy(d => d.TaskType)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var optimizedRules = new ArbitrationRules();

                foreach (var kvp in decisionsByTaskType)
                {
                    var taskType = kvp.Key;
                    var decisions = kvp.Value;

                    // Calculate model performance for this task type
                    var modelPerformance = decisions
                        .GroupBy(d => d.SelectedModelId)
                        .Select(g => new
                        {
                            ModelId = g.Key,
                            Count = g.Count(),
                            AvgProcessingTime = g.Average(d => d.SelectionDuration)
                        })
                        .OrderByDescending(m => m.Count)
                        .Take(5) // Top 5 models for this task type
                        .ToList();

                    // Create rule for this task type
                    var rule = new ArbitrationRule
                    {
                        TaskType = taskType,
                        Priority = 1,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Conditions = new List<RuleCondition>(),
                        ModelPreferences = modelPerformance
                            .Select(m => new ModelPreference
                            {
                                ModelId = m.ModelId,
                                Weight = (decimal)m.Count / decisions.Count,
                                Reason = $"Historical success rate: {m.Count}/{decisions.Count}"
                            })
                            .ToList()
                    };

                    optimizedRules.Rules.Add(rule);
                }

                // 3. Update cost optimization rules based on budget usage
                var budgetStatus = await _budgetService.GetBudgetStatusAsync(context.TenantId);
                if (budgetStatus.UtilizationPercentage > 70)
                {
                    // Add cost-saving rule
                    optimizedRules.Rules.Add(new ArbitrationRule
                    {
                        TaskType = "cost_sensitive",
                        Priority = 2,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        IsActive = true,
                        Conditions = new List<RuleCondition>
                {
                    new RuleCondition
                    {
                        Field = "budget_utilization",
                        Operator = ">",
                        Value = "70",
                        LogicalOperator = "AND"
                    }
                },
                        ModelPreferences = new List<ModelPreference>
                {
                    new ModelPreference
                    {
                        ModelId = "gpt-3.5-turbo", // Example cost-effective model
                        Weight = 0.8m,
                        Reason = "Cost optimization during high budget utilization"
                    }
                }
                    });
                }

                // 4. Save optimized rules
                await _modelRepository.SaveArbitrationRulesAsync(optimizedRules);

                _logger.LogInformation("Rule optimization completed. Generated {RuleCount} rules", optimizedRules.Rules.Count);

                return optimizedRules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rule optimization failed");
                throw;
            }
        }

        public async Task<EngineConfiguration> GetConfigurationAsync()
        {
            var configuration = new EngineConfiguration
            {
                EngineVersion = "1.0.0",
                LastUpdated = DateTime.UtcNow,
                Features = new Dictionary<string, bool>
                {
                    ["model_selection"] = true,
                    ["cost_tracking"] = true,
                    ["performance_prediction"] = _performancePredictor != null,
                    ["circuit_breaker"] = _circuitBreaker != null,
                    ["rate_limiting"] = _rateLimiter != null,
                    ["batch_processing"] = true,
                    ["streaming"] = true,
                    ["rule_optimization"] = true
                },
                Settings = new Dictionary<string, object>
                {
                    ["max_concurrent_selections"] = 5,
                    ["default_selection_strategy"] = "balanced",
                    ["fallback_model_count"] = 3,
                    ["performance_score_weights"] = new { performance = 0.4, cost = 0.3, compliance = 0.3 },
                    ["cache_ttl_minutes"] = 5,
                    ["health_check_interval_seconds"] = 30
                },
                AvailableModels = (await _modelRepository.GetActiveModelsAsync())
                    .Select(m => new ModelConfiguration
                    {
                        ModelId = m.ProviderModelId,
                        Provider = m.Provider.Name,
                        CostPerMillionInputTokens = m.CostPerMillionInputTokens,
                        CostPerMillionOutputTokens = m.CostPerMillionOutputTokens,
                        MaxTokens = m.MaxTokens,
                        IntelligenceScore = m.IntelligenceScore,
                        IsActive = m.IsActive
                    })
                    .ToList(),
                RateLimits = new RateLimitConfiguration
                {
                    MaxRequestsPerMinute = 100,
                    MaxRequestsPerHour = 1000,
                    MaxTokensPerMinute = 100000
                }
            };

            return configuration;
        }

        public async Task<EngineHealthStatus> GetHealthStatusAsync()
        {
            var healthStatus = new EngineHealthStatus
            {
                Timestamp = DateTime.UtcNow,
                OverallStatus = "Healthy",
                Uptime = GetUptime(),
                Version = "1.0.0"
            };

            // Check dependencies
            var dependencyChecks = new List<DependencyHealth>();

            try
            {
                // Check model repository
                var modelCount = await _modelRepository.GetActiveModelsCountAsync();
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "ModelRepository",
                    Status = "Healthy",
                    ResponseTime = 0,
                    Details = $"Active models: {modelCount}"
                });
            }
            catch (Exception ex)
            {
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "ModelRepository",
                    Status = "Unhealthy",
                    ErrorMessage = ex.Message,
                    ResponseTime = 0
                });
                healthStatus.OverallStatus = "Degraded";
            }

            try
            {
                // Check adapter factory
                var providers = await _adapterFactory.GetAvailableProvidersAsync();
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "ProviderAdapterFactory",
                    Status = "Healthy",
                    ResponseTime = 0,
                    Details = $"Available providers: {providers.Count}"
                });
            }
            catch (Exception ex)
            {
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "ProviderAdapterFactory",
                    Status = "Unhealthy",
                    ErrorMessage = ex.Message,
                    ResponseTime = 0
                });
                healthStatus.OverallStatus = "Degraded";
            }

            try
            {
                // Check cost tracker
                var recentUsage = await _costTracker.GetRecentUsageAsync(TimeSpan.FromHours(1));
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "CostTrackingService",
                    Status = "Healthy",
                    ResponseTime = 0,
                    Details = $"Recent requests: {recentUsage.Count}"
                });
            }
            catch (Exception ex)
            {
                dependencyChecks.Add(new DependencyHealth
                {
                    ServiceName = "CostTrackingService",
                    Status = "Unhealthy",
                    ErrorMessage = ex.Message,
                    ResponseTime = 0
                });
                healthStatus.OverallStatus = "Degraded";
            }

            // Add performance metrics
            healthStatus.PerformanceMetrics = new PerformanceMetrics
            {
                AverageSelectionTimeMs = await GetAverageSelectionTimeAsync(),
                SuccessRate = await GetSuccessRateAsync(),
                ActiveConnections = 0, // Would need connection tracking
                MemoryUsageMb = GetMemoryUsage(),
                CpuUsagePercent = GetCpuUsage()
            };

            healthStatus.Dependencies = dependencyChecks;

            // Update overall status based on critical dependencies
            var criticalDependencies = new[] { "ModelRepository", "ProviderAdapterFactory" };
            var unhealthyCritical = dependencyChecks
                .Where(d => criticalDependencies.Contains(d.ServiceName) && d.Status != "Healthy")
                .ToList();

            if (unhealthyCritical.Any())
            {
                healthStatus.OverallStatus = "Unhealthy";
            }

            return healthStatus;
        }

        #endregion

        #region Additional Helper Methods for Interface Implementation

        private async Task<StreamingModelResponse> TryFallbackStreamingAsync(ChatRequest request, ArbitrationContext context, Exception originalException)
        {
            _logger.LogInformation("Attempting fallback streaming after failure: {Error}", originalException.Message);

            try
            {
                // Get arbitration result to access fallback candidates
                var arbitrationResult = await SelectModelAsync(context);
                var fallbackCandidates = arbitrationResult.FallbackCandidates;

                foreach (var fallback in fallbackCandidates)
                {
                    try
                    {
                        _logger.LogInformation("Trying fallback model for streaming: {ModelId}", fallback.Model.ProviderModelId);

                        var adapter = await _adapterFactory.GetAdapterForModelAsync(fallback.Model.ProviderModelId);
                        var response = await adapter.SendChatCompletionStreamAsync(request with
                        {
                            ModelId = fallback.Model.ProviderModelId
                        });

                        _logger.LogInformation("Fallback streaming successful with model: {ModelId}", fallback.Model.ProviderModelId);
                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Fallback model {ModelId} also failed for streaming", fallback.Model.ProviderModelId);
                    }
                }

                throw new InvalidOperationException("All models failed for streaming, including fallbacks", originalException);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All fallback attempts failed for streaming");
                throw;
            }
        }

        private TimeSpan GetUptime()
        {
            // In a real implementation, you would track when the service started
            // For now, return a placeholder
            return TimeSpan.FromHours(1);
        }

        private async Task<double> GetAverageSelectionTimeAsync()
        {
            try
            {
                var recentDecisions = await _modelRepository.GetRecentArbitrationDecisionsAsync(
                    Guid.Empty, // All tenants
                    DateTime.UtcNow.AddHours(1));

                if (!recentDecisions.Any()) return 0;

                return recentDecisions.Average(d => d.SelectionDuration);
            }
            catch
            {
                return 0;
            }
        }

        private async Task<double> GetSuccessRateAsync()
        {
            try
            {
                var recentFailures = await _modelRepository.GetRecentRequestFailuresAsync(DateTime.UtcNow.AddHours(1));
                var recentDecisions = await _modelRepository.GetRecentArbitrationDecisionsAsync(
                    Guid.Empty,
                    DateTime.UtcNow.AddHours(1));

                if (!recentDecisions.Any()) return 100;

                var failureCount = recentFailures.Count;
                var totalRequests = recentDecisions.Count + failureCount;

                if (totalRequests == 0) return 100;

                return 100 - ((double)failureCount / totalRequests * 100);
            }
            catch
            {
                return 100;
            }
        }

        private double GetMemoryUsage()
        {
            // In a real implementation, use Process.GetCurrentProcess()
            return 0;
        }

        private double GetCpuUsage()
        {
            // In a real implementation, use PerformanceCounter
            return 0;
        }

        /// <summary>
        /// ////////////////////////////////////////////////////////////////////
        /// </summary>
        public class StreamingModelResponse
        {
            public string Id { get; set; }
            public string ModelId { get; set; }
            public IAsyncEnumerable<string> Stream { get; set; }
            public TimeSpan ProcessingTime { get; set; }
            public Func<int, int, decimal, Task> OnCompletion { get; set; }
            public bool IsSuccess { get; set; } = true;
            public string ErrorMessage { get; set; }
        }

        public class BatchExecutionResult
        {
            public string BatchId { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
            public TimeSpan TotalDuration { get; set; }
            public int TotalRequests { get; set; }
            public int SuccessfulCount { get; set; }
            public int FailedCount { get; set; }
            public decimal TotalCost { get; set; }
            public TimeSpan AverageProcessingTime { get; set; }
            public string SelectedModel { get; set; }
            public List<ModelResponse> Responses { get; set; }
            public List<FailedRequest> FailedRequests { get; set; }
            public double BatchEfficiency { get; set; }
            public string Error { get; set; }
        }

        public class FailedRequest
        {
            public ChatRequest Request { get; set; }
            public string ErrorMessage { get; set; }
            public string ErrorType { get; set; }
            public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        }

        public class ArbitrationRules
        {
            public List<ArbitrationRule> Rules { get; set; } = new List<ArbitrationRule>();
            public DateTime LastOptimized { get; set; } = DateTime.UtcNow;
            public string OptimizationStrategy { get; set; } = "historical_performance";
        }

        public class ArbitrationRule
        {
            public string Id { get; set; } = Guid.NewGuid().ToString();
            public string TaskType { get; set; }
            public int Priority { get; set; }
            public List<RuleCondition> Conditions { get; set; }
            public List<ModelPreference> ModelPreferences { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
            public bool IsActive { get; set; }
        }

        public class RuleCondition
        {
            public string Field { get; set; }
            public string Operator { get; set; } // ">", "<", "=", "contains", etc.
            public string Value { get; set; }
            public string LogicalOperator { get; set; } // "AND", "OR"
        }

        public class ModelPreference
        {
            public string ModelId { get; set; }
            public decimal Weight { get; set; }
            public string Reason { get; set; }
        }

        public class EngineConfiguration
        {
            public string EngineVersion { get; set; }
            public DateTime LastUpdated { get; set; }
            public Dictionary<string, bool> Features { get; set; }
            public Dictionary<string, object> Settings { get; set; }
            public List<ModelConfiguration> AvailableModels { get; set; }
            public RateLimitConfiguration RateLimits { get; set; }
        }

        public class ModelConfiguration
        {
            public string ModelId { get; set; }
            public string Provider { get; set; }
            public decimal CostPerMillionInputTokens { get; set; }
            public decimal CostPerMillionOutputTokens { get; set; }
            public int MaxTokens { get; set; }
            public int IntelligenceScore { get; set; }
            public bool IsActive { get; set; }
        }

        public class RateLimitConfiguration
        {
            public int MaxRequestsPerMinute { get; set; }
            public int MaxRequestsPerHour { get; set; }
            public int MaxTokensPerMinute { get; set; }
        }

        public class EngineHealthStatus
        {
            public string OverallStatus { get; set; } // Healthy, Degraded, Unhealthy
            public DateTime Timestamp { get; set; }
            public TimeSpan Uptime { get; set; }
            public string Version { get; set; }
            public List<DependencyHealth> Dependencies { get; set; }
            public PerformanceMetrics PerformanceMetrics { get; set; }
        }

        public class DependencyHealth
        {
            public string ServiceName { get; set; }
            public string Status { get; set; }
            public long ResponseTime { get; set; } // in milliseconds
            public string ErrorMessage { get; set; }
            public string Details { get; set; }
        }

        public class PerformanceMetrics
        {
            public double AverageSelectionTimeMs { get; set; }
            public double SuccessRate { get; set; }
            public int ActiveConnections { get; set; }
            public double MemoryUsageMb { get; set; }
            public double CpuUsagePercent { get; set; }
        }


        #endregion
    }

    #region Supporting Classes

    // Define missing classes referenced in the code
    public class TokenEstimation
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class ModelPerformanceMetric
    {
        public Guid ModelId { get; set; }
        public double LatencyMs { get; set; }
        public double SuccessRate { get; set; }
        public double TokensPerSecond { get; set; }
        public DateTime Timestamp { get; set; }
        public double ErrorRate { get; set; }
    }

    public class RequestFailure
    {
        public Guid TenantId { get; set; }
        public Guid UserId { get; set; }
        public Guid? ProjectId { get; set; }
        public string TaskType { get; set; }
        public string ErrorMessage { get; set; }
        public string ErrorType { get; set; }
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public string ContextData { get; set; }
    }

    #endregion


}







//using Microsoft.Extensions.Logging;
//using System.Diagnostics;
//using System.Text.Json;
//using AIArbitration.Core.Interfaces;
//using AIArbitration.Core.Entities;
//using AIArbitration.Core.Models;
//using AIArbitration.Core.Services;
//using AIArbitration.Core.Entities.Enums;

//namespace AIArbitration.Infrastructure.Services
//{
//    public class ArbitrationEngine : IArbitrationEngine
//    {
//        private readonly IModelRepository _modelRepository;
//        private readonly IProviderAdapterFactory _adapterFactory;
//        private readonly ICostTrackingService _costTracker;
//        private readonly IPerformancePredictor _performancePredictor;
//        private readonly IUserService _userService;
//        private readonly ITenantService _tenantService;
//        private readonly IBudgetService _budgetService;
//        private readonly IComplianceService _complianceService;
//        private readonly ILogger<ArbitrationEngine> _logger;
//        private readonly IRateLimiter _rateLimiter;
//        private readonly ICircuitBreaker _circuitBreaker;

//        public ArbitrationEngine(
//            IModelRepository modelRepository,
//            IProviderAdapterFactory adapterFactory,
//            ICostTrackingService costTracker,
//            IPerformancePredictor performancePredictor,
//            IUserService userService,
//            ITenantService tenantService,
//            IBudgetService budgetService,
//            IComplianceService complianceService,
//            ILogger<ArbitrationEngine> logger,
//            IRateLimiter rateLimiter,
//            ICircuitBreaker circuitBreaker)
//        {
//            _modelRepository = modelRepository;
//            _adapterFactory = adapterFactory;
//            _costTracker = costTracker;
//            _performancePredictor = performancePredictor;
//            _userService = userService;
//            _tenantService = tenantService;
//            _budgetService = budgetService;
//            _complianceService = complianceService;
//            _logger = logger;
//            _rateLimiter = rateLimiter;
//            _circuitBreaker = circuitBreaker;
//        }

//        public async Task<ArbitrationResult> SelectModelAsync(ArbitrationContext context)
//        {
//            var decisionId = Guid.NewGuid().ToString();
//            _logger.LogInformation("Starting model selection {DecisionId} for user {UserId}", decisionId, context.UserId);

//            try
//            {
//                // 1. Check rate limits
//                await _rateLimiter.CheckRateLimitAsync(context);

//                // 2. Get all eligible models
//                var candidates = await GetCandidatesAsync(context);

//                if (!candidates.Any())
//                    throw new NoSuitableModelException("No suitable models found");

//                // 3. Score and rank candidates
//                var scoredCandidates = await ScoreAndRankCandidatesAsync(candidates, context);

//                // 4. Select best model
//                var selectedModel = SelectBestModel(scoredCandidates, context);

//                // 5. Get performance prediction
//                var performance = await PredictPerformanceAsync(context);

//                // 6. Estimate cost
//                var costEstimation = await EstimateCostForModelAsync(selectedModel.Model, context);

//                // 7. Prepare fallback candidates
//                var fallbackCandidates = scoredCandidates
//                    .Where(c => c.Model.Id != selectedModel.Model.Id)
//                    .OrderByDescending(c => c.FinalScore)
//                    .Take(3)
//                    .ToList();

//                // 8. Record decision
//                await RecordArbitrationDecisionAsync(decisionId, context, selectedModel, scoredCandidates);

//                return new ArbitrationResult
//                {
//                    SelectedModel = selectedModel,
//                    AllCandidates = scoredCandidates,
//                    FallbackCandidates = fallbackCandidates,
//                    EstimatedCost = costEstimation,
//                    PerformancePrediction = performance,
//                    Timestamp = DateTime.UtcNow,
//                    DecisionId = decisionId,
//                    DecisionFactors = new Dictionary<string, object>
//                    {
//                        ["primary_factor"] = context.TaskType,
//                        ["budget_constrained"] = context.MaxCost.HasValue,
//                        ["compliance_requirements"] = context.RequireDataResidency || context.RequireEncryptionAtRest,
//                        ["candidate_count"] = scoredCandidates.Count
//                    },
//                    ExcludedModels = scoredCandidates
//                        .Where(c => c.FinalScore < 50)
//                        .Select(c => c.Model.ProviderModelId)
//                        .ToList(),
//                    SelectionStrategy = DetermineSelectionStrategy(context)
//                };
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Model selection failed {DecisionId}", decisionId);
//                throw;
//            }
//        }

//        public async Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context)
//        {
//            var stopwatch = Stopwatch.StartNew();

//            try
//            {
//                // 1. Select model
//                var arbitrationResult = await SelectModelAsync(context);
//                var selectedModel = arbitrationResult.SelectedModel;

//                // 2. Get adapter for selected provider
//                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

//                // 3. Execute with circuit breaker
//                var response = await _circuitBreaker.ExecuteAsync(async () =>
//                {
//                    var result = await adapter.SendChatCompletionAsync(request with
//                    {
//                        ModelId = selectedModel.Model.ProviderModelId
//                    });

//                    return result;
//                }, selectedModel.Model.Provider.Name);

//                stopwatch.Stop();

//                // 4. Update model performance metrics
//                await UpdateModelPerformanceAsync(selectedModel.Model.Id, response.ProcessingTime, true);

//                // 5. Record usage for billing
//                await _costTracker.RecordUsageAsync(new UsageRecord
//                {
//                    TenantId = context.TenantId,
//                    UserId = context.UserId,
//                    ProjectId = context.ProjectId,
//                    ModelId = selectedModel.Model.ProviderModelId,
//                    Provider = selectedModel.Model.Provider.Name,
//                    InputTokens = response.InputTokens,
//                    OutputTokens = response.OutputTokens,
//                    Cost = response.Cost,
//                    ProcessingTime = response.ProcessingTime,
//                    Timestamp = DateTime.UtcNow,
//                    RequestId = response.Id,
//                    Success = true
//                });

//                // 6. Check for budget warnings
//                await CheckBudgetWarningsAsync(context, response.Cost);

//                return response;
//            }
//            catch (Exception ex)
//            {
//                stopwatch.Stop();

//                // Record failure
//                await RecordFailedRequestAsync(context, ex, stopwatch.Elapsed);

//                // Try fallback if available
//                return await TryFallbackExecutionAsync(request, context, ex);
//            }
//        }

//        public async Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context)
//        {
//            var models = await _modelRepository.GetActiveModelsAsync();
//            var candidates = new List<ArbitrationCandidate>();

//            foreach (var model in models)
//            {
//                try
//                {
//                    // Check basic eligibility
//                    if (!await IsModelEligibleAsync(model, context))
//                        continue;

//                    var candidate = await CreateCandidateAsync(model, context);
//                    candidates.Add(candidate);
//                }
//                catch (Exception ex)
//                {
//                    _logger.LogWarning(ex, "Failed to evaluate model {ModelId}", model.ProviderModelId);
//                }
//            }

//            return candidates;
//        }

//        public async Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context)
//        {
//            var candidates = await GetCandidatesAsync(context);

//            if (!candidates.Any())
//                throw new NoSuitableModelException("No models available for cost estimation");

//            // Estimate tokens based on request
//            var tokenEstimation = await EstimateTokensAsync(request);

//            var estimations = new List<CostEstimation>();

//            foreach (var candidate in candidates.Take(5)) // Limit to top 5
//            {
//                var estimation = new CostEstimation
//                {
//                    EstimatedInputTokens = tokenEstimation.InputTokens,
//                    EstimatedOutputTokens = tokenEstimation.OutputTokens,
//                    InputCost = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens,
//                    OutputCost = (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
//                    EstimatedCost = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens +
//                                  (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
//                    CostBreakdown = new Dictionary<string, decimal>
//                    {
//                        ["input_cost"] = (tokenEstimation.InputTokens / 1_000_000m) * candidate.Model.CostPerMillionInputTokens,
//                        ["output_cost"] = (tokenEstimation.OutputTokens / 1_000_000m) * candidate.Model.CostPerMillionOutputTokens,
//                        ["model_license"] = candidate.Model.Tier == ModelTier.Premium ? 0.001m : 0m,
//                        ["infrastructure"] = 0.0005m
//                    }
//                };

//                estimations.Add(estimation);
//            }

//            // Return the average estimation
//            return new CostEstimation
//            {
//                EstimatedCost = estimations.Average(e => e.EstimatedCost),
//                InputCost = estimations.Average(e => e.InputCost),
//                OutputCost = estimations.Average(e => e.OutputCost),
//                EstimatedInputTokens = tokenEstimation.InputTokens,
//                EstimatedOutputTokens = tokenEstimation.OutputTokens,
//                CostBreakdown = estimations
//                    .SelectMany(e => e.CostBreakdown)
//                    .GroupBy(kv => kv.Key)
//                    .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value))
//            };
//        }

//        public async Task<PerformancePrediction> PredictPerformanceAsync(ArbitrationContext context)
//        {
//            var candidates = await GetCandidatesAsync(context);

//            if (!candidates.Any())
//                throw new NoSuitableModelException("No models available for performance prediction");

//            var predictions = new List<PerformancePrediction>();

//            foreach (var candidate in candidates.Take(3))
//            {
//                var prediction = await _performancePredictor.PredictAsync(candidate.Model, context);
//                predictions.Add(prediction);
//            }

//            // Return best prediction
//            return predictions.OrderByDescending(p => p.ReliabilityScore).First();
//        }

//        private async Task<bool> IsModelEligibleAsync(AIModel model, ArbitrationContext context)
//        {
//            // Check basic criteria
//            if (context.MinIntelligenceScore.HasValue &&
//                model.IntelligenceScore < context.MinIntelligenceScore.Value)
//                return false;

//            // Check user/tenant restrictions
//            var userConstraints = await _userService.GetUserConstraintsAsync(context.UserId);
//            if (userConstraints.BlockedModels.Contains(model.ProviderModelId))
//                return false;

//            // Check provider health
//            var providerHealth = await _modelRepository.GetProviderHealthAsync(model.ProviderId);
//            if (providerHealth?.Status != ProviderHealthStatus.Healthy)
//                return false;

//            // Check compliance
//            var complianceCheck = await _complianceService.CheckModelComplianceAsync(model, context);
//            if (!complianceCheck.IsCompliant)
//                return false;

//            return true;
//        }

//        private async Task<ArbitrationCandidate> CreateCandidateAsync(AIModel model, ArbitrationContext context)
//        {
//            // Calculate scores
//            var performanceScore = await CalculatePerformanceScoreAsync(model);
//            var costScore = await CalculateCostScoreAsync(model, context);
//            var complianceScore = await CalculateComplianceScoreAsync(model, context);

//            // Weighted final score
//            var finalScore = (performanceScore * 0.4m) +
//                            (costScore * 0.3m) +
//                            (complianceScore * 0.3m);

//            return new ArbitrationCandidate
//            {
//                Model = model,
//                TotalCost = await CalculateExpectedCostAsync(model, context),
//                PerformanceScore = performanceScore,
//                ComplianceScore = complianceScore,
//                ValueScore = model.IntelligenceScore / await CalculateExpectedCostAsync(model, context),
//                FinalScore = finalScore,
//                ProviderEndpoint = model.Provider.BaseUrl,
//                EstimatedLatency = await EstimateLatencyAsync(model),
//                ReliabilityScore = await CalculateReliabilityScoreAsync(model)
//            };
//        }

//        private async Task<decimal> CalculatePerformanceScoreAsync(AIModel model)
//        {
//            var metrics = await _modelRepository.GetModelPerformanceMetricsAsync(model.Id);

//            if (!metrics.Any()) return 50m; // Default score

//            var latencyScore = CalculateLatencyScore(metrics.Average(m => m.LatencyMs));
//            var successRateScore = metrics.Average(m => m.SuccessRate) * 100;
//            var throughputScore = CalculateThroughputScore(metrics.Average(m => m.TokensPerSecond));

//            return (latencyScore * 0.4m) + (successRateScore * 0.4m) + (throughputScore * 0.2m);
//        }

//        private async Task<decimal> CalculateCostScoreAsync(AIModel model, ArbitrationContext context)
//        {
//            var expectedCost = await CalculateExpectedCostAsync(model, context);

//            // Lower cost = higher score (inverted)
//            if (expectedCost <= 0) return 100m;

//            // Normalize cost (assuming $10 is max expected cost per request)
//            var normalizedCost = Math.Min(expectedCost / 10m, 1m);
//            return 100m * (1m - normalizedCost);
//        }

//        private async Task<CostEstimation> EstimateCostForModelAsync(AIModel model, ArbitrationContext context)
//        {
//            // Get average token usage for this task type
//            var avgTokens = await GetAverageTokenUsageAsync(context.TaskType);

//            return new CostEstimation
//            {
//                EstimatedInputTokens = avgTokens.Input,
//                EstimatedOutputTokens = avgTokens.Output,
//                InputCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens,
//                OutputCost = (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens,
//                EstimatedCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens +
//                              (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens
//            };
//        }

//        private async Task RecordArbitrationDecisionAsync(
//            string decisionId,
//            ArbitrationContext context,
//            ArbitrationCandidate selectedModel,
//            List<ArbitrationCandidate> allCandidates)
//        {
//            // Store decision in database for analytics
//            var decision = new ArbitrationDecision
//            {
//                Id = decisionId,
//                TenantId = context.TenantId,
//                UserId = context.UserId,
//                ProjectId = context.ProjectId,
//                SelectedModelId = selectedModel.Model.Id,
//                TaskType = context.TaskType,
//                CandidateCount = allCandidates.Count,
//                SelectionDuration = 0, // Will be set by caller
//                Timestamp = DateTime.UtcNow,
//                DecisionFactorsJson = JsonSerializer.Serialize(new
//                {
//                    context.MinIntelligenceScore,
//                    context.MaxCost,
//                    context.RequiredRegion,
//                    RequiredCapabilities = context.RequiredCapabilities.Keys.ToList()
//                })
//            };

//            await _modelRepository.RecordArbitrationDecisionAsync(decision);
//        }

//        private string DetermineSelectionStrategy(ArbitrationContext context)
//        {
//            if (context.MaxCost.HasValue && context.MaxCost < 0.10m)
//                return "cost_optimized";

//            if (context.MinIntelligenceScore.HasValue && context.MinIntelligenceScore > 70)
//                return "intelligence_optimized";

//            if (context.MaxLatency.HasValue && context.MaxLatency < TimeSpan.FromSeconds(2))
//                return "latency_optimized";

//            if (context.RequiredCapabilities.Any())
//                return "capability_optimized";

//            return "balanced";
//        }
//    }
//}
#endregion
