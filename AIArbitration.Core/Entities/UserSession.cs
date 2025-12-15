using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class UserSession
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // User & Tenant Information
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        // Session Information
        public string SessionToken { get; set; } = string.Empty; // Hashed/Encrypted session token
        public string RefreshToken { get; set; } = string.Empty; // Hashed/Encrypted refresh token
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastActivityAt { get; set; }
        public SessionStatus Status { get; set; } = SessionStatus.Active;

        // Authentication Context
        public AuthenticationMethod AuthMethod { get; set; }
        public string? AuthProvider { get; set; } // "local", "google", "azuread", etc.
        public bool MfaUsed { get; set; }
        public string? MfaMethod { get; set; } // "totp", "sms", "email", "webauthn"
        public string? MfaDeviceId { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Device Information
        public string? DeviceId { get; set; }
        public string? DeviceType { get; set; } // "desktop", "mobile", "tablet", "cli"
        public string? DeviceName { get; set; }
        public string? DeviceModel { get; set; }
        public string? DeviceVendor { get; set; }

        // Browser/Client Information
        public string? UserAgent { get; set; }
        public string? Browser { get; set; }
        public string? BrowserVersion { get; set; }
        public string? OperatingSystem { get; set; }
        public string? OsVersion { get; set; }
        public string? Platform { get; set; }

        // Network Information
        public string? IPAddress { get; set; }
        public string? Country { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public string? Timezone { get; set; }

        // Application Context
        public string? Application { get; set; } = "web"; // "web", "mobile", "cli", "api"
        public string? ClientVersion { get; set; }
        public string? ClientId { get; set; } // OAuth client ID

        // Security Information
        public string? SecurityToken { get; set; } // JWT or other security token
        public string[] Scopes { get; set; } = Array.Empty<string>();
        public string[] Permissions { get; set; } = Array.Empty<string>();
        public string? TokenIssuer { get; set; }
        public DateTime? TokenIssuedAt { get; set; }
        public DateTime? TokenExpiresAt { get; set; }

        // Session Termination
        public DateTime? TerminatedAt { get; set; }
        public string? TerminationReason { get; set; } // "logout", "expired", "revoked", "security", "inactivity"
        public string? TerminatedBy { get; set; } // "user", "admin", "system"

        // Performance Metrics
        public int RequestCount { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public decimal? AverageLatency { get; set; }

        // Risk Assessment
        public RiskLevel SessionRisk { get; set; } = RiskLevel.Low;
        public bool IsSuspicious { get; set; }
        public string[] RiskFactors { get; set; } = Array.Empty<string>();

        // Compliance
        public string? ConsentVersion { get; set; }
        public DateTime? ConsentAcceptedAt { get; set; }
        public string? PrivacyPolicyVersion { get; set; }
        public DateTime? PrivacyPolicyAcceptedAt { get; set; }

        // Navigation Properties
        public virtual ApplicationUser User { get; set; } = null!;
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ICollection<ApiRequest> Requests { get; set; } = new List<ApiRequest>();
    }
}
