using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    // Update PerformancePrediction to add missing properties and relationships
    // Add PredictionModel for ML model storage
    public class PredictionModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty; // "latency", "success_rate", "cost"
        public string Algorithm { get; set; } = string.Empty; // "linear_regression", "random_forest", "neural_network"
        public byte[] ModelData { get; set; } = Array.Empty<byte>(); // Serialized ML model
        public Dictionary<string, double> FeatureWeights { get; set; } = new();
        public Dictionary<string, string> Parameters { get; set; } = new();
        public decimal Accuracy { get; set; }
        public DateTime TrainedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;

        // Training metrics
        public double RMSE { get; set; }
        public double MAE { get; set; }
        public double R2 { get; set; }
        public int TrainingSamples { get; set; }
    }
}
