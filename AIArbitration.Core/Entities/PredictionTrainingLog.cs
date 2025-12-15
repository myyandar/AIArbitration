namespace AIArbitration.Core.Entities
{
    // Add PredictionTrainingLog
    public class PredictionTrainingLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ModelId { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public DateTime TrainingDate { get; set; } = DateTime.UtcNow;
        public int TrainingSamples { get; set; }
        public TimeSpan TrainingDuration { get; set; }
        public Dictionary<string, double> Metrics { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public virtual PredictionModel Model { get; set; } = null!;
    }
}
