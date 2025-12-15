using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Core.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IPerformancePredictor
    {
        // Model performance prediction
        Task<PerformancePrediction> PredictAsync(AIModel model, ArbitrationContext context);
        Task<PerformancePrediction> PredictAsync(string modelId, ArbitrationContext context);

        // Batch predictions
        Task<Dictionary<string, PerformancePrediction>> PredictBatchAsync(List<string> modelIds, ArbitrationContext context);

        // Historical analysis
        Task<PerformanceAnalysis> AnalyzeHistoricalPerformanceModelAsync(string modelId, DateTime start, DateTime end);
        Task<PerformanceAnalysis> AnalyzeHistoricalPerformanceProviderAsync(string providerId, DateTime start, DateTime end);
        
        // Trend analysis
        Task<PerformanceTrend> GetPerformanceTrendAsync(string modelId, TimeSpan lookbackPeriod);
        Task<List<PerformanceAnomaly>> DetectAnomaliesAsync(string modelId, DateTime start, DateTime end);

        // Model comparison
        Task<PerformanceComparison> CompareModelsAsync(List<string> modelIds, ArbitrationContext context);

        // Training and updates
        Task TrainModelAsync(List<PerformanceDataPoint> trainingData);
        Task UpdateModelAsync(string modelId, PerformanceDataPoint newDataPoint);
    }
}
