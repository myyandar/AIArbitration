namespace AIArbitration.Core.Entities
{
    public class ComplianceImprovement
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Priority { get; set; } = "Medium"; // Low, Medium, High, Critical
        public string Category { get; set; } = string.Empty;
        public List<string> AffectedStandards { get; set; } = new();
        public string Status { get; set; } = "pending"; // pending, in_progress, completed, deferred
        public DateTime? DueDate { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
        public string? CompletedBy { get; set; }
    }
}
