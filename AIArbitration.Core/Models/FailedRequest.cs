using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class FailedRequest
    {
        public ChatRequest Request { get; set; } = null!;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;
        public string? ModelAttempted { get; set; }
        public string? ProviderAttempted { get; set; }
        public DateTime FailedAt { get; set; } = DateTime.UtcNow;
        public Dictionary<string, object>? Context { get; set; }
    }
}
