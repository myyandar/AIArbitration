namespace AIArbitration.Core.Models
{
    public class BudgetForecast
    {
        public string BudgetId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public DateTime ForecastDate { get; set; } = DateTime.UtcNow;
        public int ForecastDays { get; set; }
        public decimal CurrentUsage { get; set; }
        public decimal BudgetAmount { get; set; }
        public decimal ForecastedUsage { get; set; }
        public decimal ForecastedRemaining => BudgetAmount - ForecastedUsage;
        public bool WillExceedBudget { get; set; }
        public DateTime? PredictedExhaustionDate { get; set; }
        public decimal ConfidenceLevel { get; set; } // 0-1
        public List<DailyForecast> DailyForecasts { get; set; } = new();
        public Dictionary<string, object> Metrics { get; set; } = new();
    }
}
