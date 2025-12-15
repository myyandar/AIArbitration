namespace AIArbitration.Core.Entities
{
    // Add DataRegion for data residency tracking
    public class DataRegion
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Code { get; set; } = string.Empty; // "eu-west-1", "us-east-2", etc.
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> ComplianceStandards { get; set; } = new(); // ["GDPR", "HIPAA"]
        public bool IsEnabled { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
