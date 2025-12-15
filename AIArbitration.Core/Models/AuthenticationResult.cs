using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class AuthenticationResult
    {
        public bool Success { get; set; }
        public string? Token { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public ApplicationUser? User { get; set; }
        public UserSession? Session { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorCode { get; set; }
        public bool RequiresMfa { get; set; }
        public string? MfaMethod { get; set; }
        public ApiKey ApiKey { get; set; }
    }
}
