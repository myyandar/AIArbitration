using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class UpdateSessionCommand
    {
        public string? IPAddress { get; set; }
        public string? UserAgent { get; set; }
        public bool ExtendExpiration { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public SessionStatus? Status { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }
}
