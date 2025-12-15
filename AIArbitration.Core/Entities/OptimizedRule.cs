using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class OptimizedRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string TaskType { get; set; } = string.Empty;
        public int Priority { get; set; }
        public List<RuleCondition> Conditions { get; set; } = new();
        public List<ModelPreference> ModelPreferences { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
