using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CreateSessionCommand
    {
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public required string IPAddress { get; set; }
        public required string UserAgent { get; set; }
        public AuthenticationMethod AuthMethod { get; set; }
        public string? DeviceId { get; set; }
        public string? DeviceType { get; set; }
        public bool MfaUsed { get; set; }
        public string? MfaMethod { get; set; }
        public TimeSpan? SessionDuration { get; set; }
    }
}
