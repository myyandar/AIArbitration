using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class DataHandlingResult
    {
        public string RequestId { get; set; } = string.Empty;
        public DataRequestType Type { get; set; }
        public bool Success { get; set; }
        public string? Data { get; set; } // For access/portability requests
        public int? RecordsAffected { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }
}
