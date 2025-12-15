namespace AIArbitration.Core.Models
{
    public class CreateProjectCommand
    {
        public required string TenantId { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? OwnerId { get; set; }
        public decimal? MonthlyBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public List<string>? AllowedModels { get; set; }
        public List<string>? BlockedModels { get; set; }
        public Dictionary<string, object>? Settings { get; set; }
    }
}
