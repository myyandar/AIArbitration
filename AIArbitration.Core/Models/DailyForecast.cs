namespace AIArbitration.Core.Models
{
    public class DailyForecast
    {
        public DateTime Date { get; set; }
        public decimal ForecastedUsage { get; set; }
        public decimal MinUsage { get; set; }
        public decimal MaxUsage { get; set; }
        public decimal Confidence { get; set; }
    }
}
