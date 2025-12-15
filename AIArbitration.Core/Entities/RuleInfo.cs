using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class RuleInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
