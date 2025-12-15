using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public enum RateLimitItem
    {
        RequestsPerSecond,
        RequestsPerMinute,
        RequestsPerHour,
        RequestsPerDay,
        TokensPerMinute,
        TokensPerHour,
        TokensPerDay,
        CostPerMinute,
        CostPerHour,
        CostPerDay,
        ConcurrentRequests,
        Custom
    }
}
