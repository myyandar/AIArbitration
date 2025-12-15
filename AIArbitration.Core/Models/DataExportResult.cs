namespace AIArbitration.Core.Models
{
    public class DataExportResult
    {
        public string UserId { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string? ExportFilePath { get; set; }
        public string? ExportFileUrl { get; set; }
        public int TotalRecords { get; set; }
        public Dictionary<string, int> RecordsByType { get; set; } = new();
        public string? ErrorMessage { get; set; }
        public DateTime ExportedAt { get; set; } = DateTime.UtcNow;
    }
}
