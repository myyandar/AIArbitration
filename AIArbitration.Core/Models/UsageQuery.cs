using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class UsageQuery
    {
        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
        public DateTime EndDate { get; set; } = DateTime.UtcNow;
        public string? TenantId { get; set; }
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string? ModelId { get; set; }
        public string? ProviderId { get; set; }
        public bool? Success { get; set; }
        public string? Operation { get; set; }
        public int? Limit { get; set; } = 100;
        public int? Offset { get; set; }
        public string? SortBy { get; set; }
        public bool? SortDescending { get; set; }
        public Dictionary<string, object>? Filters { get; set; }
    }
}
