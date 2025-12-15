using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IRateLimiter
    {
        // Rate limit checking
        Task<RateLimitResult> CheckRateLimitAsync(ArbitrationContext context);
        Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitType type);

        // Rate limit configuration
        Task<RateLimitConfiguration> GetRateLimitConfigurationAsync(string identifier, RateLimitType type);
        Task UpdateRateLimitConfigurationAsync(string identifier, RateLimitConfiguration configuration);

        // Usage tracking
        Task<RateLimitUsage> GetRateLimitUsageAsync(string identifier, RateLimitType type);
        Task<List<RateLimitUsage>> GetRateLimitUsagesAsync(string tenantId, DateTime start, DateTime end);

        // Quota management
        Task<RateLimitQuota> GetRateLimitQuotaAsync(string identifier);
        Task UpdateRateLimitQuotaAsync(string identifier, RateLimitQuota quota);

        // Violation tracking
        Task RecordRateLimitViolationAsync(RateLimitViolation violation);
        Task<List<RateLimitViolation>> GetRateLimitViolationsAsync(string tenantId, DateTime start, DateTime end);

        // Reset and cleanup
        Task ResetRateLimitAsync(string identifier, RateLimitType type);
        Task CleanupOldRateLimitsAsync(DateTime olderThan);

        // Basic rate limiting operations
        Task<bool> AllowRequestAsync(string key, int weight = 1);
        Task<int> GetRemainingRequestsAsync(string key);
        Task<DateTime> GetResetTimeAsync(string key);
        Task RecordRequestAsync(string key, int weight = 1);
    }
}

//public interface IRateLimiter
//{
//    // Rate limit checking
//    Task<RateLimitResult> CheckRateLimitAsync(ArbitrationContext context);
//    Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitItem type);

//    // Rate limit configuration
//    Task<RateLimitConfiguration> GetRateLimitConfigurationAsync(string identifier, RateLimitItem type);
//    Task UpdateRateLimitConfigurationAsync(string identifier, RateLimitConfiguration configuration);

//    // Usage tracking
//    Task<RateLimitUsage> GetRateLimitUsageAsync(string identifier, RateLimitItem type);
//    Task<List<RateLimitUsage>> GetRateLimitUsagesAsync(string tenantId, DateTime start, DateTime end);

//    // Quota management
//    Task<RateLimitQuota> GetRateLimitQuotaAsync(string identifier);
//    Task UpdateRateLimitQuotaAsync(string identifier, RateLimitQuota quota);

//    // Violation tracking
//    Task RecordRateLimitViolationAsync(RateLimitViolation violation);
//    Task<List<RateLimitViolation>> GetRateLimitViolationsAsync(string tenantId, DateTime start, DateTime end);

//    // Reset and cleanup
//    Task ResetRateLimitAsync(string identifier, RateLimitItem type);
//    Task CleanupOldRateLimitsAsync(DateTime olderThan);
//    Task<bool> AllowRequestAsync(string key, int weight = 1);
//    Task<int> GetRemainingRequestsAsync(string key);
//    Task<DateTime> GetResetTimeAsync(string key);
//    Task RecordRequestAsync(string key, int weight = 1);
//}