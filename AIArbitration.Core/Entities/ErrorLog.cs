using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ErrorLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string RequestId { get; set; } = string.Empty;
        public string ProviderId { get; set; } = string.Empty;
        public string ModelId { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double? DurationMs { get; set; }
        public bool? Success { get; set; }
        public string? Error { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime CreatedAt { get; set; }
        public Object EntityType { get; set; }
        public string EntityTypeName { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; }
        public string Changes { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public Dictionary<string, object> Metadata { get; set; } = new();
    }
}
