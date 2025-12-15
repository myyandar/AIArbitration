using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ApiKey
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string KeyHash { get; set; } = string.Empty;
        public string Prefix { get; set; } = string.Empty; // First 8 chars for identification
        public string? UserId { get; set; }
        public string? ProjectId { get; set; }
        public string TenantId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;

        // Permissions
        public string PermissionsJson { get; set; } = "[]";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; } = true;

        // Rate limiting
        public int RateLimitPerMinute { get; set; } = 60;
        public int RateLimitPerDay { get; set; } = 10000;

        // Navigation
        public virtual ApplicationUser? User { get; set; }
        public virtual Project? Project { get; set; }
        public virtual Tenant Tenant { get; set; } = null!;
        public string? SessionId { get; set; }

        // Navigation
        public virtual UserSession? Session { get; set; }
    }
}
