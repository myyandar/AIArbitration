namespace AIArbitration.Core.Entities
{
    // Add PredictionFeature for feature engineering
    public class PredictionFeature
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string FeatureType { get; set; } = string.Empty; // "numeric", "categorical", "temporal"
        public bool IsEnabled { get; set; } = true;
        public double Importance { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
