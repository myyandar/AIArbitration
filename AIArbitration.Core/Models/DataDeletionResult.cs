namespace AIArbitration.Core.Models
{
    public class DataDeletionResult
    {
        public string UserId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int RecordsDeleted { get; set; }
        public List<string> TablesAffected { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime DeletedAt { get; set; } = DateTime.UtcNow;
    }
}
