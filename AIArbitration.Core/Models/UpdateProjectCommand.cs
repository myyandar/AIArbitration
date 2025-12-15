namespace AIArbitration.Core.Models
{
    public class UpdateProjectCommand
    {
        public string ProjectId { get; set; }
        public string TenantId { get; set; }
        public string? Name { get; set; }
        public string? Description { get; set; }
        public string? OwnerId { get; set; }
        public decimal? MonthlyBudget { get; set; }
        public decimal? DailyBudget { get; set; }
        public bool? IsActive { get; set; }
        public List<string>? AllowedModels { get; set; }
        public List<string>? BlockedModels { get; set; }
    }
}
