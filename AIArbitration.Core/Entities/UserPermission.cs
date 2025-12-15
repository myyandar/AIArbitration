using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class UserPermission
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PermissionType Type { get; set; }
        public string Resource { get; set; } = string.Empty; // e.g., "models:read", "budgets:write"
        public bool IsAllowed { get; set; } = true;
        public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
        public string? GrantedBy { get; set; }

        // Navigation
        public virtual ApplicationUser User { get; set; } = null!;
    }
}
