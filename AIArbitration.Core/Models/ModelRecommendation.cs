namespace AIArbitration.Core.Models
{
    public class ModelRecommendation
    {
        public string ModelId { get; set; } = string.Empty;
        public decimal SuccessRate { get; set; }
        public double AverageDuration { get; set; }
        public int SampleSize { get; set; }
    }
}
