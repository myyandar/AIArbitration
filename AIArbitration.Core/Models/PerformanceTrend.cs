namespace AIArbitration.Core.Models
{
    public class PerformanceTrend
    {
        public string ModelId { get; set; } = string.Empty;
        public TimeSpan LookbackPeriod { get; set; }
        public List<PerformanceDataPoint> DataPoints { get; set; } = new();
        public decimal LatencyTrend { get; set; } // Positive = increasing latency, Negative = decreasing
        public decimal SuccessRateTrend { get; set; } // Positive = improving, Negative = declining
        public decimal CostTrend { get; set; } // Positive = increasing cost, Negative = decreasing
        public bool IsStable { get; set; }
        public string StabilityAssessment { get; set; } = string.Empty;
        public List<DateTime> ChangePoints { get; set; } = new();
    }
}
