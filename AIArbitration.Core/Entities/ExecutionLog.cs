using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ExecutionLog
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string TenantId { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        public string? ProjectId { get; set; }
        public string? ModelId { get; set; }
        public string? Provider { get; set; }
        public string TaskType { get; set; } = "general";
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal Cost { get; set; }

        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public string? ErrorType { get; set; }
        public string? RequestMetadata { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
