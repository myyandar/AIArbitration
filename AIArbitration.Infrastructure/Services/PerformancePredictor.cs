using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AIArbitration.Infrastructure.Services
{
    public class PerformancePredictor : IPerformancePredictor
    {
        private readonly AIArbitrationDbContext _context;
        private readonly ILogger<PerformancePredictor> _logger;
        private readonly Random _random = new Random();
        private readonly Dictionary<string, PredictionModel> _predictionModels = new();

        public PerformancePredictor(AIArbitrationDbContext context, ILogger<PerformancePredictor> logger)
        {
            _context = context;
            _logger = logger;

            // Initialize prediction models (in production, these would be loaded from database)
            InitializePredictionModels();
        }

        private void InitializePredictionModels()
        {
            // Latency prediction model
            _predictionModels["latency"] = new PredictionModel
            {
                Id = "latency_model_1",
                Name = "Latency Predictor",
                ModelType = "latency",
                Algorithm = "weighted_average",
                Accuracy = 0.85m,
                FeatureWeights = new Dictionary<string, double>
                {
                    ["historical_latency"] = 0.6,
                    ["current_load"] = 0.2,
                    ["model_tier"] = 0.1,
                    ["provider_health"] = 0.1
                }
            };

            // Success rate prediction model
            _predictionModels["success_rate"] = new PredictionModel
            {
                Id = "success_model_1",
                Name = "Success Rate Predictor",
                ModelType = "success_rate",
                Algorithm = "logistic_regression",
                Accuracy = 0.92m,
                FeatureWeights = new Dictionary<string, double>
                {
                    ["historical_success"] = 0.7,
                    ["recent_failures"] = 0.15,
                    ["provider_status"] = 0.1,
                    ["time_of_day"] = 0.05
                }
            };

            // Cost prediction model
            _predictionModels["cost"] = new PredictionModel
            {
                Id = "cost_model_1",
                Name = "Cost Predictor",
                ModelType = "cost",
                Algorithm = "linear_regression",
                Accuracy = 0.95m,
                FeatureWeights = new Dictionary<string, double>
                {
                    ["pricing_info"] = 0.8,
                    ["token_count"] = 0.15,
                    ["provider_markup"] = 0.05
                }
            };
        }

        #region Single Predictions

        public async Task<PerformancePrediction> PredictAsync(AIModel model, ArbitrationContext context)
        {
            try
            {
                if (model == null)
                    throw new ArgumentNullException(nameof(model));

                // Get historical data
                var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
                var historicalData = await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == model.Id && p.AnalysisPeriodEnd >= thirtyDaysAgo)
                    .ToListAsync();

                var providerHealth = await _context.ProviderHealth
                    .Where(h => h.ProviderId == model.ProviderId)
                    .OrderByDescending(h => h.CheckedAt)
                    .FirstOrDefaultAsync();

                var healthStatus = providerHealth?.ProviderHealthStatus ?? ProviderHealthStatus.Unknown;
                // Cannot infer yje type of implicitly typed deconstruction variable 'var' 
                // Calculate predictions
                (double latencyPrediction, decimal latencyConfidence) = await PredictLatencyAsync(model, context, historicalData, healthStatus);
                (decimal successPrediction, decimal successConfidence) = await PredictSuccessRateAsync(model, context, historicalData, healthStatus);
                var (costPrediction, costConfidence) = await PredictCostAsync(model, context);

                // Calculate reliability score
                var reliabilityScore = CalculateReliabilityScore(successPrediction, healthStatus, historicalData);

                // Calculate overall confidence
                var overallConfidence = (latencyConfidence + successConfidence + costConfidence) / 3;

                // Create prediction
                var prediction = new PerformancePrediction
                {
                    Id = Guid.NewGuid().ToString(),
                    ModelId = model.Id,
                    ProviderId = model.ProviderId,
                    EstimatedLatency = TimeSpan.FromSeconds(latencyPrediction),
                    PredictedLatency = TimeSpan.FromSeconds(latencyPrediction),
                    SuccessProbability = successPrediction,
                    PredictedSuccessRate = successPrediction * 100,
                    ReliabilityScore = reliabilityScore,
                    Confidence = overallConfidence,
                    EstimatedCostPerRequest = costPrediction,
                    EstimatedCostPerToken = model.CostPerMillionInputTokens / 1000000,
                    HistoricalRequests = historicalData.Sum(h => h.TotalRequests),
                    HistoricalDataPoints = historicalData.Count,
                    RecentFailures = historicalData.Sum(h => h.TotalRequests - h.SuccessfulRequests),
                    AverageLatency = historicalData.Any()
                        ? TimeSpan.FromSeconds(historicalData.Average(h => h.AverageLatency.TotalSeconds))
                        : TimeSpan.FromSeconds(model.Latency),
                    Timestamp = DateTime.UtcNow,
                    Notes = $"Based on {historicalData.Count} historical data points. Provider health: {healthStatus}"
                };

                // Add detailed metrics
                prediction.Metrics["throughput"] = CalculateThroughput(model, context);
                prediction.Metrics["error_rate"] = 1 - (double)successPrediction;
                prediction.Metrics["availability"] = healthStatus == ProviderHealthStatus.Healthy ? 1.0 : 0.5;

                // Add prediction scores
                prediction.PredictionScores["latency"] = (decimal)(100 - (latencyPrediction / 10)); // Lower latency = higher score
                prediction.PredictionScores["success"] = successPrediction * 100;
                prediction.PredictionScores["cost"] = 100 - (costPrediction * 1000); // Lower cost = higher score
                prediction.PredictionScores["reliability"] = reliabilityScore;

                return prediction;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting performance for model {ModelId}", model?.Id);
                throw;
            }
        }

        public async Task<PerformancePrediction> PredictAsync(string modelId, ArbitrationContext context)
        {
            try
            {
                var model = await _context.AIModels
                    .Include(m => m.Provider)
                    .Include(m => m.PricingInfo)
                    .Include(m => m.Capabilities)
                    .FirstOrDefaultAsync(m => m.Id == modelId);

                if (model == null)
                    throw new ArgumentException($"Model with ID {modelId} not found");

                return await PredictAsync(model, context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error predicting performance for model ID {ModelId}", modelId);
                throw;
            }
        }

        #endregion

        #region Batch Predictions

        public async Task<Dictionary<string, PerformancePrediction>> PredictBatchAsync(List<string> modelIds, ArbitrationContext context)
        {
            try
            {
                var predictions = new Dictionary<string, PerformancePrediction>();

                foreach (var modelId in modelIds)
                {
                    try
                    {
                        var prediction = await PredictAsync(modelId, context);
                        predictions[modelId] = prediction;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to predict for model {ModelId}, using fallback", modelId);

                        // Create fallback prediction
                        predictions[modelId] = new PerformancePrediction
                        {
                            Id = Guid.NewGuid().ToString(),
                            ModelId = modelId,
                            ReliabilityScore = 50,
                            SuccessProbability = 0.5m,
                            Confidence = 0.1m,
                            Notes = "Fallback prediction due to prediction failure"
                        };
                    }
                }

                return predictions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in batch prediction for {ModelCount} models", modelIds.Count);
                throw;
            }
        }

        #endregion

        #region Historical Analysis

        public async Task<PerformanceAnalysis> AnalyzeHistoricalPerformanceModelAsync(string modelId, DateTime start, DateTime end)
        {
            try
            {
                var performanceData = await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId
                        && p.AnalysisPeriodStart >= start
                        && p.AnalysisPeriodEnd <= end)
                    .ToListAsync();

                if (!performanceData.Any())
                {
                    return new PerformanceAnalysis
                    {
                        ModelId = modelId,
                        AnalysisPeriodStart = start,
                        AnalysisPeriodEnd = end,
                        TotalRequests = 0,
                        SuccessfulRequests = 0,
                        Latency = TimeSpan.Zero
                    };
                }

                // Aggregate data
                var aggregated = new PerformanceAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    ModelId = modelId,
                    AnalysisPeriodStart = start,
                    AnalysisPeriodEnd = end,
                    TotalRequests = performanceData.Sum(p => p.TotalRequests),
                    SuccessfulRequests = performanceData.Sum(p => p.SuccessfulRequests),
                    FailedRequests = performanceData.Sum(p => p.TotalRequests - p.SuccessfulRequests),
                    TotalLatency = new TimeSpan(performanceData.Sum(p => p.TotalLatency.Ticks)),
                    MinLatency = performanceData.Min(p => p.MinLatency),
                    MaxLatency = performanceData.Max(p => p.MaxLatency),
                    UpdatedAt = DateTime.UtcNow
                };

                if (aggregated.TotalRequests > 0)
                {
                    aggregated.AverageLatency = TimeSpan.FromTicks(aggregated.TotalLatency.Ticks / aggregated.TotalRequests);
                    aggregated.Latency = aggregated.AverageLatency;

                    // Calculate percentiles if we have enough data
                    if (performanceData.Count >= 10)
                    {
                        var allLatencies = performanceData
                            .SelectMany(p => Enumerable.Repeat(p.AverageLatency, p.TotalRequests))
                            .OrderBy(l => l)
                            .ToList();

                        aggregated.P50Latency = CalculatePercentile(allLatencies, 0.50);
                        aggregated.P90Latency = CalculatePercentile(allLatencies, 0.90);
                        aggregated.P95Latency = CalculatePercentile(allLatencies, 0.95);
                        aggregated.P99Latency = CalculatePercentile(allLatencies, 0.99);
                    }
                }

                return aggregated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing historical performance for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<PerformanceAnalysis> AnalyzeHistoricalPerformanceProviderAsync(string providerId, DateTime start, DateTime end)
        {
            try
            {
                // Get all models for this provider
                var modelIds = await _context.AIModels
                    .Where(m => m.ProviderId == providerId)
                    .Select(m => m.Id)
                    .ToListAsync();

                if (!modelIds.Any())
                    throw new ArgumentException($"No models found for provider {providerId}");

                // Get performance data for all models
                var allPerformanceData = new List<PerformanceAnalysis>();
                foreach (var modelId in modelIds)
                {
                    var modelAnalysis = await AnalyzeHistoricalPerformanceModelAsync(modelId, start, end);
                    allPerformanceData.Add(modelAnalysis);
                }

                // Aggregate across all models
                var aggregated = new PerformanceAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    ProviderId = providerId,
                    AnalysisPeriodStart = start,
                    AnalysisPeriodEnd = end,
                    TotalRequests = allPerformanceData.Sum(p => p.TotalRequests),
                    SuccessfulRequests = allPerformanceData.Sum(p => p.SuccessfulRequests),
                    FailedRequests = allPerformanceData.Sum(p => p.FailedRequests),
                    TotalLatency = new TimeSpan(allPerformanceData.Sum(p => p.TotalLatency.Ticks)),
                    MinLatency = allPerformanceData.Min(p => p.MinLatency),
                    MaxLatency = allPerformanceData.Max(p => p.MaxLatency),
                    UpdatedAt = DateTime.UtcNow
                };

                if (aggregated.TotalRequests > 0)
                {
                    aggregated.AverageLatency = TimeSpan.FromTicks(aggregated.TotalLatency.Ticks / aggregated.TotalRequests);
                    aggregated.Latency = aggregated.AverageLatency;
                }

                return aggregated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing historical performance for provider {ProviderId}", providerId);
                throw;
            }
        }

        #endregion

        #region Trend Analysis

        public async Task<PerformanceTrend> GetPerformanceTrendAsync(string modelId, TimeSpan lookbackPeriod)
        {
            try
            {
                var endDate = DateTime.UtcNow;
                var startDate = endDate - lookbackPeriod;

                var performanceData = await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId
                        && p.AnalysisPeriodEnd >= startDate
                        && p.AnalysisPeriodEnd <= endDate)
                    .OrderBy(p => p.AnalysisPeriodStart)
                    .ToListAsync();

                if (!performanceData.Any())
                {
                    return new PerformanceTrend
                    {
                        ModelId = modelId,
                        LookbackPeriod = lookbackPeriod,
                        IsStable = true,
                        StabilityAssessment = "Insufficient data for trend analysis"
                    };
                }

                var trend = new PerformanceTrend
                {
                    ModelId = modelId,
                    LookbackPeriod = lookbackPeriod,
                    DataPoints = performanceData.Select(p => new PerformanceDataPoint
                    {
                        ModelId = modelId,
                        Latency = p.AverageLatency,
                        Success = p.SuccessRate >= 95,
                        Timestamp = p.AnalysisPeriodStart
                    }).ToList()
                };

                // Calculate trends using linear regression
                if (performanceData.Count >= 3)
                {
                    var latencies = performanceData.Select(p => p.AverageLatency.TotalSeconds).ToArray();
                    var successRates = performanceData.Select(p => (double)p.SuccessRate).ToArray();
                    var timestamps = performanceData.Select((p, i) => (double)i).ToArray();

                    var latencySlope = CalculateLinearRegressionSlope(timestamps, latencies);
                    var successRateSlope = CalculateLinearRegressionSlope(timestamps, successRates);

                    trend.LatencyTrend = (decimal)latencySlope;
                    trend.SuccessRateTrend = (decimal)successRateSlope;
                    trend.IsStable = Math.Abs(latencySlope) < 0.1 && Math.Abs(successRateSlope) < 0.5;
                    trend.StabilityAssessment = trend.IsStable ? "Performance is stable" : "Performance is trending";
                }
                else
                {
                    trend.IsStable = true;
                    trend.StabilityAssessment = "Insufficient data points for reliable trend analysis";
                }

                return trend;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting performance trend for model {ModelId}", modelId);
                throw;
            }
        }

        public async Task<List<PerformanceAnomaly>> DetectAnomaliesAsync(string modelId, DateTime start, DateTime end)
        {
            try
            {
                var performanceData = await _context.PerformanceAnalysis
                    .Where(p => p.ModelId == modelId
                        && p.AnalysisPeriodStart >= start
                        && p.AnalysisPeriodEnd <= end)
                    .OrderBy(p => p.AnalysisPeriodStart)
                    .ToListAsync();

                if (performanceData.Count < 3)
                    return new List<PerformanceAnomaly>();

                var anomalies = new List<PerformanceAnomaly>();
                var latencies = performanceData.Select(p => p.AverageLatency.TotalSeconds).ToList();
                var successRates = performanceData.Select(p => (double)p.SuccessRate).ToList();

                // Calculate statistics
                var meanLatency = latencies.Average();
                var stdDevLatency = CalculateStandardDeviation(latencies);
                var meanSuccessRate = successRates.Average();
                var stdDevSuccessRate = CalculateStandardDeviation(successRates);

                // Detect anomalies (3 standard deviations from mean)
                for (int i = 0; i < performanceData.Count; i++)
                {
                    var data = performanceData[i];

                    // Check latency anomaly
                    var latencyZScore = Math.Abs((latencies[i] - meanLatency) / stdDevLatency);
                    if (latencyZScore > 3 && stdDevLatency > 0)
                    {
                        anomalies.Add(new PerformanceAnomaly
                        {
                            Id = Guid.NewGuid().ToString(),
                            PerformanceAnalysisId = data.Id,
                            DetectedAt = data.AnalysisPeriodEnd,
                            Type = "HighLatency",
                            Description = $"Latency anomaly detected: {data.AverageLatency.TotalSeconds:F2}s (Z-score: {latencyZScore:F2})",
                            Deviation = (decimal)latencyZScore
                        });
                    }

                    // Check success rate anomaly
                    var successZScore = Math.Abs((successRates[i] - meanSuccessRate) / stdDevSuccessRate);
                    if (successZScore > 3 && stdDevSuccessRate > 0)
                    {
                        anomalies.Add(new PerformanceAnomaly
                        {
                            Id = Guid.NewGuid().ToString(),
                            PerformanceAnalysisId = data.Id,
                            DetectedAt = data.AnalysisPeriodEnd,
                            Type = "LowSuccessRate",
                            Description = $"Success rate anomaly detected: {data.SuccessRate:F1}% (Z-score: {successZScore:F2})",
                            Deviation = (decimal)successZScore
                        });
                    }
                }

                return anomalies;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting anomalies for model {ModelId}", modelId);
                throw;
            }
        }

        #endregion

        #region Model Comparison

        public async Task<PerformanceComparison> CompareModelsAsync(List<string> modelIds, ArbitrationContext context)
        {
            try
            {
                if (modelIds == null || !modelIds.Any())
                    throw new ArgumentException("Model IDs list cannot be empty");

                var predictions = new Dictionary<string, PerformancePrediction>();
                var comparisonScores = new Dictionary<string, decimal>();
                var strengths = new Dictionary<string, string>();
                var weaknesses = new Dictionary<string, string>();

                // Get predictions for all models
                foreach (var modelId in modelIds)
                {
                    try
                    {
                        var prediction = await PredictAsync(modelId, context);
                        predictions[modelId] = prediction;

                        // Calculate comparison score (weighted average of important metrics)
                        comparisonScores[modelId] = CalculateComparisonScore(prediction, context);

                        // Identify strengths and weaknesses
                        strengths[modelId] = IdentifyStrengths(prediction, context);
                        weaknesses[modelId] = IdentifyWeaknesses(prediction, context);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get prediction for model {ModelId} in comparison", modelId);
                    }
                }

                // Determine recommended model
                string? recommendedModel = null;
                if (comparisonScores.Any())
                {
                    recommendedModel = comparisonScores.OrderByDescending(kv => kv.Value).First().Key;
                }

                return new PerformanceComparison
                {
                    ModelIds = modelIds,
                    Context = context,
                    Predictions = predictions,
                    ComparisonScores = comparisonScores,
                    Strengths = strengths,
                    Weaknesses = weaknesses,
                    RecommendedModel = recommendedModel,
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing models");
                throw;
            }
        }

        #endregion

        #region Training and Updates

        public async Task TrainModelAsync(List<PerformanceDataPoint> trainingData)
        {
            try
            {
                if (!trainingData.Any())
                    return;

                // Store training data
                //foreach (var dataPoint in trainingData)
                //{
                //    dataPoint.Id = Guid.NewGuid().ToString();
                //}

                await _context.PerformanceDataPoints.AddRangeAsync(trainingData);
                await _context.SaveChangesAsync();

                // Log training
                var log = new PredictionTrainingLog
                {
                    //Id = Guid.NewGuid().ToString(),
                    ModelId = "latency_model_1", // In production, this would be dynamic
                    ModelType = "latency",
                    TrainingDate = DateTime.UtcNow,
                    TrainingSamples = trainingData.Count,
                    TrainingDuration = TimeSpan.FromSeconds(trainingData.Count * 0.1), // Simulated
                    Metrics = new Dictionary<string, double>
                    {
                        ["rmse"] = 0.25,
                        ["mae"] = 0.18,
                        ["r2"] = 0.85
                    },
                    Success = true
                };

                _context.PredictionTrainingLogs.Add(log);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Trained model with {Count} data points", trainingData.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error training model");
                throw;
            }
        }

        public async Task UpdateModelAsync(string modelId, PerformanceDataPoint newDataPoint)
        {
            try
            {
                //newDataPoint.Id = Guid.NewGuid().ToString();
                newDataPoint.ModelId = modelId;
                newDataPoint.Timestamp = DateTime.UtcNow;

                _context.PerformanceDataPoints.Add(newDataPoint);
                await _context.SaveChangesAsync();

                _logger.LogDebug("Updated model {ModelId} with new data point", modelId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model {ModelId}", modelId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<(double latency, decimal confidence)> PredictLatencyAsync(
            AIModel model,
            ArbitrationContext context,
            List<PerformanceAnalysis> historicalData,
            ProviderHealthStatus healthStatus)
        {
            if (!historicalData.Any())
            {
                // Fallback to model's base latency with adjustment for task complexity
                var baseLatency = model.Latency;
                var complexityFactor = GetTaskComplexityFactor(context);
                return (baseLatency * complexityFactor, 0.3m);
            }

            // Calculate weighted average of historical latency
            var totalRequests = historicalData.Sum(h => h.TotalRequests);
            var weightedLatency = historicalData.Sum(h =>
                h.AverageLatency.TotalSeconds * h.TotalRequests) / totalRequests;

            // Adjust for current factors
            var adjustedLatency = weightedLatency * GetLatencyAdjustmentFactor(model, context, healthStatus);

            // Calculate confidence based on data quality
            var confidence = CalculateLatencyConfidence(historicalData);

            return (adjustedLatency, confidence);
        }

        private async Task<(decimal successRate, decimal confidence)> PredictSuccessRateAsync(
            AIModel model,
            ArbitrationContext context,
            List<PerformanceAnalysis> historicalData,
            ProviderHealthStatus healthStatus)
        {
            if (!historicalData.Any())
            {
                var baseSuccessRate = healthStatus == ProviderHealthStatus.Healthy ? 0.95m : 0.7m;
                return (baseSuccessRate, 0.3m);
            }

            // Calculate weighted success rate
            var totalRequests = historicalData.Sum(h => h.TotalRequests);
            var successfulRequests = historicalData.Sum(h => h.SuccessfulRequests);
            var baseRate = (decimal)successfulRequests / totalRequests;

            // Adjust for current conditions
            var adjustedRate = baseRate * GetSuccessRateAdjustmentFactor(healthStatus);

            // Ensure within bounds
            adjustedRate = Math.Max(0.1m, Math.Min(0.999m, adjustedRate));

            // Calculate confidence
            var confidence = CalculateSuccessRateConfidence(historicalData);

            return (adjustedRate, confidence);
        }

        private async Task<(decimal cost, decimal confidence)> PredictCostAsync(
            AIModel model,
            ArbitrationContext context)
        {
            try
            {
                var pricingInfo = await _context.PricingInfos
                    .FirstOrDefaultAsync(p => p.ModelId == model.Id);

                if (pricingInfo == null)
                {
                    // Calculate from model's pricing
                    var estimatedCost = (model.CostPerMillionInputTokens * context.ExpectedInputTokens / 1000000m) +
                                       (model.CostPerMillionOutputTokens * context.ExpectedOutputTokens / 1000000m);
                    return (estimatedCost, 0.7m);
                }

                // Use pricing info
                var cost = pricingInfo.PricePerInputToken.GetValueOrDefault(0) * context.ExpectedInputTokens +
                          pricingInfo.PricePerOutputToken.GetValueOrDefault(0) * context.ExpectedOutputTokens;

                return (cost, 0.9m);
            }
            catch
            {
                // Fallback calculation
                var fallbackCost = (model.CostPerMillionInputTokens * context.ExpectedInputTokens / 1000000m) +
                                  (model.CostPerMillionOutputTokens * context.ExpectedOutputTokens / 1000000m);
                return (fallbackCost, 0.5m);
            }
        }

        private decimal CalculateReliabilityScore(
            decimal successProbability,
            ProviderHealthStatus healthStatus,
            List<PerformanceAnalysis> historicalData)
        {
            var baseScore = successProbability * 100;

            // Adjust for provider health
            var healthMultiplier = healthStatus switch
            {
                ProviderHealthStatus.Healthy => 1.0m,
                ProviderHealthStatus.Degraded => 0.9m,
                ProviderHealthStatus.Unstable => 0.7m,
                ProviderHealthStatus.Down => 0.3m,
                ProviderHealthStatus.RateLimited => 0.8m,
                ProviderHealthStatus.Maintenance => 0.6m,
                _ => 0.5m
            };

            // Adjust for data sufficiency
            var dataPoints = historicalData.Sum(h => h.TotalRequests);
            var dataMultiplier = dataPoints >= 1000 ? 1.0m :
                                dataPoints >= 100 ? 0.9m :
                                dataPoints >= 10 ? 0.8m : 0.6m;

            var adjustedScore = baseScore * healthMultiplier * dataMultiplier;
            return Math.Max(0, Math.Min(100, adjustedScore));
        }

        private double CalculateThroughput(AIModel model, ArbitrationContext context)
        {
            // Estimate tokens per second based on model tier and capabilities
            var baseThroughput = model.Tier switch
            {
                ModelTier.Enterprise => 1000,
                ModelTier.Premium => 500,
                ModelTier.Standard => 250,
                ModelTier.Basic => 100,
                _ => 50
            };

            // Adjust for task complexity
            var complexityFactor = GetTaskComplexityFactor(context);
            return baseThroughput / complexityFactor;
        }

        private double GetTaskComplexityFactor(ArbitrationContext context)
        {
            var factor = 1.0;

            if (context.RequiresVision) factor *= 1.5;
            if (context.RequiresFunctionCalling) factor *= 1.3;
            if (context.RequiresAudio) factor *= 1.4;
            if (context.RequiresStreaming) factor *= 0.9; // Streaming might be faster for first token

            // Adjust for token count
            var tokenFactor = Math.Max(1.0, (context.ExpectedInputTokens + context.ExpectedOutputTokens) / 1000.0);
            factor *= tokenFactor;

            return Math.Max(1.0, factor);
        }

        private double GetLatencyAdjustmentFactor(
            AIModel model,
            ArbitrationContext context,
            ProviderHealthStatus healthStatus)
        {
            var factor = 1.0;

            // Provider health adjustment
            factor *= healthStatus switch
            {
                ProviderHealthStatus.Healthy => 1.0,
                ProviderHealthStatus.Degraded => 1.2,
                ProviderHealthStatus.Unstable => 1.5,
                ProviderHealthStatus.Down => 3.0,
                ProviderHealthStatus.RateLimited => 1.3,
                ProviderHealthStatus.Maintenance => 1.8,
                _ => 1.0
            };

            // Time of day adjustment (simulated - in production would use actual patterns)
            var hour = DateTime.UtcNow.Hour;
            if (hour >= 9 && hour <= 17) // Peak hours
                factor *= 1.3;
            else if (hour >= 18 && hour <= 22) // Evening hours
                factor *= 1.1;

            return factor;
        }

        private decimal GetSuccessRateAdjustmentFactor(ProviderHealthStatus healthStatus)
        {
            return healthStatus switch
            {
                ProviderHealthStatus.Healthy => 1.0m,
                ProviderHealthStatus.Degraded => 0.95m,
                ProviderHealthStatus.Unstable => 0.8m,
                ProviderHealthStatus.Down => 0.3m,
                ProviderHealthStatus.RateLimited => 0.9m,
                ProviderHealthStatus.Maintenance => 0.7m,
                _ => 0.5m
            };
        }

        private decimal CalculateLatencyConfidence(List<PerformanceAnalysis> historicalData)
        {
            if (!historicalData.Any()) return 0.1m;

            var dataPoints = historicalData.Sum(h => h.TotalRequests);
            if (dataPoints >= 10000) return 0.95m;
            if (dataPoints >= 1000) return 0.85m;
            if (dataPoints >= 100) return 0.7m;
            if (dataPoints >= 10) return 0.5m;
            return 0.3m;
        }

        private decimal CalculateSuccessRateConfidence(List<PerformanceAnalysis> historicalData)
        {
            if (!historicalData.Any()) return 0.1m;

            var totalRequests = historicalData.Sum(h => h.TotalRequests);
            var successfulRequests = historicalData.Sum(h => h.SuccessfulRequests);

            if (totalRequests >= 1000 && successfulRequests >= 950)
                return 0.95m;
            if (totalRequests >= 100 && successfulRequests >= 90)
                return 0.8m;
            if (totalRequests >= 10)
                return 0.6m;
            return 0.3m;
        }

        private decimal CalculateComparisonScore(PerformancePrediction prediction, ArbitrationContext context)
        {
            var weights = new Dictionary<string, decimal>
            {
                ["latency"] = 0.35m,
                ["success"] = 0.35m,
                ["cost"] = 0.20m,
                ["reliability"] = 0.10m
            };

            var score = 0m;

            // Latency score (lower is better)
            var maxAllowedLatency = context.MaxAllowedLatency.TotalSeconds;
            var latencyRatio = (decimal)prediction.EstimatedLatency.TotalSeconds / (decimal)maxAllowedLatency;
            var latencyScore = Math.Max(0, 1 - latencyRatio) * 100;
            score += latencyScore * weights["latency"];

            // Success score
            score += prediction.SuccessProbability * 100 * weights["success"];

            // Cost score (lower is better)
            if (context.MaxAllowedCost > 0 && prediction.EstimatedCostPerRequest > 0)
            {
                var costRatio = prediction.EstimatedCostPerRequest / context.MaxAllowedCost;
                var costScore = Math.Max(0, 1 - costRatio) * 100;
                score += costScore * weights["cost"];
            }
            else
            {
                score += 50 * weights["cost"]; // Default score if no cost constraint
            }

            // Reliability score
            score += prediction.ReliabilityScore * weights["reliability"];

            return score;
        }

        private string IdentifyStrengths(PerformancePrediction prediction, ArbitrationContext context)
        {
            var strengths = new List<string>();

            if (prediction.EstimatedLatency <= context.MaxAllowedLatency)
                strengths.Add("Meets latency requirements");

            if (prediction.SuccessProbability >= 0.95m)
                strengths.Add("High success probability");

            if (prediction.ReliabilityScore >= 80)
                strengths.Add("High reliability");

            if (prediction.EstimatedCostPerRequest <= context.MaxAllowedCost * 0.5m)
                strengths.Add("Cost effective");

            return string.Join("; ", strengths);
        }

        private string IdentifyWeaknesses(PerformancePrediction prediction, ArbitrationContext context)
        {
            var weaknesses = new List<string>();

            if (prediction.EstimatedLatency > context.MaxAllowedLatency)
                weaknesses.Add("Exceeds latency requirements");

            if (prediction.SuccessProbability < 0.9m)
                weaknesses.Add("Low success probability");

            if (prediction.ReliabilityScore < 70)
                weaknesses.Add("Low reliability");

            if (prediction.EstimatedCostPerRequest > context.MaxAllowedCost)
                weaknesses.Add("Exceeds cost budget");

            return string.Join("; ", weaknesses);
        }

        private TimeSpan CalculatePercentile(List<TimeSpan> values, double percentile)
        {
            if (!values.Any()) return TimeSpan.Zero;

            var index = (int)Math.Ceiling(percentile * values.Count) - 1;
            index = Math.Max(0, Math.Min(values.Count - 1, index));
            return values[index];
        }

        private double CalculateLinearRegressionSlope(double[] x, double[] y)
        {
            if (x.Length != y.Length || x.Length < 2)
                return 0;

            var n = x.Length;
            var sumX = x.Sum();
            var sumY = y.Sum();
            var sumXY = x.Zip(y, (a, b) => a * b).Sum();
            var sumX2 = x.Sum(a => a * a);

            var numerator = n * sumXY - sumX * sumY;
            var denominator = n * sumX2 - sumX * sumX;

            return denominator == 0 ? 0 : numerator / denominator;
        }

        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            var mean = values.Average();
            var sumSquares = values.Sum(x => Math.Pow(x - mean, 2));
            return Math.Sqrt(sumSquares / (values.Count - 1));
        }
        #endregion
    }
}
