using AIArbitration.Core.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class ComplianceValidationResult
    {
        public string EntityId { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty; // "tenant", "project", "user"
        public bool IsCompliant { get; set; }
        public List<ComplianceCheckResult> CheckResults { get; set; } = new();
        public List<string> Violations { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }
}
