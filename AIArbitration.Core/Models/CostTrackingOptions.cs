namespace AIArbitration.Core.Models
{
    public class CostTrackingOptions
    {
        public int CurrencyPrecision { get; set; } = 6;
        public int MaxQueryRangeDays { get; set; } = 365;
        public int MaxUsageRecordsQuery { get; set; } = 10000;
        public int MaxInvoiceQuery { get; set; } = 1000;
        public decimal DefaultPricePerInputToken { get; set; } = 0.000001m;
        public decimal DefaultPricePerOutputToken { get; set; } = 0.000002m;
        public decimal DefaultTaxRate { get; set; } = 0.1m; // 10%
        public decimal ServiceFeeRate { get; set; } = 0.05m; // 5%
        public bool EnableCostCaching { get; set; } = true;
        public int CacheDurationMinutes { get; set; } = 30;
    }
}
