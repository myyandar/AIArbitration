using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class UserStatistics
    {
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime GeneratedAt { get; set; }
        public decimal TotalCost { get; set; }
        public int TotalRequests { get; set; }
        public int ActiveApiKeys { get; set; }
        public int ActiveSessions { get; set; }
    }
}
