using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class UserRole
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;

        // Permissions (stored as JSON for flexibility)
        public string PermissionsJson { get; set; } = "[]";

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
    }
}
