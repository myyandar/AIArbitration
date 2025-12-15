using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Infrastructure.Services
{
    public class RedisRateLimiter : IRateLimiter
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisRateLimiter> _logger;
        private readonly TimeSpan _defaultWindow = TimeSpan.FromMinutes(1);
        private const int DefaultRequestLimit = 100;
        private const int DefaultTokenLimit = 1000;

        public RedisRateLimiter(IConnectionMultiplexer multiplexer, ILogger<RedisRateLimiter> logger)
        {
            _db = multiplexer?.GetDatabase() ?? throw new ArgumentNullException(nameof(multiplexer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // -----------------------------
        // Public interface implementations
        // -----------------------------
        public async Task<RateLimitResult> CheckRateLimitAsync(ArbitrationContext context)
        {
            var identifier = GetIdentifier(context);
            return await CheckRateLimitAsync(identifier, RateLimitType.Request);
        }

        public async Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitType type)
        {
            var config = await GetRateLimitConfigurationAsync(identifier, type);
            var now = DateTime.UtcNow;
            var windowStart = GetWindowStart(now, config.Window);

            var zsetKey = GetStateKey(identifier, type);

            // Remove old entries (score < windowStartTicks)
            var windowStartTicks = new DateTimeOffset(windowStart).ToUnixTimeMilliseconds();
            await _db.SortedSetRemoveRangeByScoreAsync(zsetKey, double.NegativeInfinity, windowStartTicks - 1);

            // Count current entries in window
            var currentCount = (int)await _db.SortedSetLengthAsync(zsetKey);

            var remaining = config.MaxRequests - currentCount;
            var resetTime = windowStart + config.Window;

            var result = new RateLimitResult
            {
                IsAllowed = currentCount < config.MaxRequests,
                CurrentCount = currentCount,
                MaxCount = config.MaxRequests,
                Remaining = Math.Max(0, remaining),
                ResetTime = resetTime,
                Window = config.Window,
                Identifier = identifier,
                Type = type,
                Message = currentCount < config.MaxRequests ? "Allowed" : "Rate limit exceeded"
            };

            if (result.IsAllowed)
            {
                var nowTicks = new DateTimeOffset(now).ToUnixTimeMilliseconds();
                // Use member value as ticks string to keep uniqueness; score is ticks too
                await _db.SortedSetAddAsync(zsetKey, nowTicks.ToString(), nowTicks);
                // Optionally set TTL on the key to avoid stale keys (window + buffer)
                await _db.KeyExpireAsync(zsetKey, config.Window.Add(TimeSpan.FromMinutes(5)));
                _logger.LogDebug("Rate limit passed for {Identifier} ({Item}): {Current}/{Max}", identifier, type, currentCount + 1, config.MaxRequests);
            }
            else
            {
                _logger.LogWarning("Rate limit exceeded for {Identifier} ({Item}): {Current}/{Max}", identifier, type, currentCount, config.MaxRequests);
                await RecordRateLimitViolationAsync(new RateLimitViolation
                {
                    Identifier = identifier,
                    Type = type,
                    CurrentCount = currentCount,
                    MaxCount = config.MaxRequests,
                    ViolatedAt = now,
                    ResetTime = resetTime
                });
            }

            return result;
        }

        // -----------------------------
        // Configuration
        // -----------------------------
        public async Task<RateLimitConfiguration> GetRateLimitConfigurationAsync(string identifier, RateLimitType type)
        {
            var key = GetConfigKey(identifier, type);
            var json = await _db.StringGetAsync(key);
            if (json.HasValue)
            {
                try
                {
                    return JsonSerializer.Deserialize<RateLimitConfiguration>((string)json)!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize rate limit configuration for {Key}", key);
                }
            }

            // default
            return new RateLimitConfiguration
            {
                Identifier = identifier,
                Type = type,
                MaxRequests = type == RateLimitType.Request ? DefaultRequestLimit : DefaultTokenLimit,
                Window = _defaultWindow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsEnabled = true
            };
        }

        public async Task UpdateRateLimitConfigurationAsync(string identifier, RateLimitConfiguration configuration)
        {
            var key = GetConfigKey(identifier, configuration.Type);
            configuration.UpdatedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(configuration);
            await _db.StringSetAsync(key, json);
            _logger.LogInformation("Updated rate limit configuration for {Identifier} ({Item})", identifier, configuration.Type);
        }

        // -----------------------------
        // Usage tracking
        // -----------------------------
        public async Task<RateLimitUsage> GetRateLimitUsageAsync(string identifier, RateLimitType type)
        {
            var config = await GetRateLimitConfigurationAsync(identifier, type);
            var zsetKey = GetStateKey(identifier, type);

            var now = DateTime.UtcNow;
            var windowStart = GetWindowStart(now, config.Window);
            var windowStartTicks = new DateTimeOffset(windowStart).ToUnixTimeMilliseconds();

            // Remove old entries
            await _db.SortedSetRemoveRangeByScoreAsync(zsetKey, double.NegativeInfinity, windowStartTicks - 1);

            var current = (int)await _db.SortedSetLengthAsync(zsetKey);
            var remaining = Math.Max(0, config.MaxRequests - current);

            return new RateLimitUsage
            {
                Identifier = identifier,
                Type = type,
                CurrentCount = current,
                MaxCount = config.MaxRequests,
                Remaining = remaining,
                PercentageUsed = config.MaxRequests > 0 ? (decimal)current / config.MaxRequests * 100 : 0,
                WindowStart = windowStart,
                WindowEnd = windowStart + config.Window,
                ResetTime = windowStart + config.Window,
                CheckedAt = DateTime.UtcNow
            };
        }

        public async Task<List<RateLimitUsage>> GetRateLimitUsagesAsync(string tenantId, DateTime start, DateTime end)
        {
            // This implementation assumes identifiers are prefixed with tenantId|...
            // We need a way to enumerate keys; Redis SCAN is required. Keep it simple: use KEYS in small deployments or maintain an index.
            var usages = new List<RateLimitUsage>();

            // WARNING: KEYS is blocking on large datasets. For production, maintain an index set of identifiers per tenant.
            var server = GetServer();
            var pattern = $"{tenantId}|state|*";
            foreach (var key in server.Keys(pattern: pattern))
            {
                var parts = key.ToString().Split('|');
                if (parts.Length < 3) continue;
                var identifier = parts[0];
                if (!Enum.TryParse<RateLimitType>(parts[2], out var type)) continue;

                var usage = await GetRateLimitUsageAsync(identifier, type);
                // Filter by time window if needed (start/end)
                usages.Add(usage);
            }

            return usages;
        }

        // -----------------------------
        // Quota management
        // -----------------------------
        public async Task<RateLimitQuota> GetRateLimitQuotaAsync(string identifier)
        {
            var requestConfig = await GetRateLimitConfigurationAsync(identifier, RateLimitType.Request);
            var tokenConfig = await GetRateLimitConfigurationAsync(identifier, RateLimitType.Token);

            return new RateLimitQuota
            {
                Identifier = identifier,
                RequestLimit = requestConfig.MaxRequests,
                RequestWindow = requestConfig.Window,
                TokenLimit = tokenConfig.MaxRequests,
                TokenWindow = tokenConfig.Window,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public async Task UpdateRateLimitQuotaAsync(string identifier, RateLimitQuota quota)
        {
            await UpdateRateLimitConfigurationAsync(identifier, new RateLimitConfiguration
            {
                Identifier = identifier,
                Type = RateLimitType.Request,
                MaxRequests = quota.RequestLimit,
                Window = quota.RequestWindow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsEnabled = true
            });

            await UpdateRateLimitConfigurationAsync(identifier, new RateLimitConfiguration
            {
                Identifier = identifier,
                Type = RateLimitType.Token,
                MaxRequests = quota.TokenLimit,
                Window = quota.TokenWindow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsEnabled = true
            });

            _logger.LogInformation("Updated rate limit quota for {Identifier}", identifier);
        }

        // -----------------------------
        // Violation tracking
        // -----------------------------
        public async Task RecordRateLimitViolationAsync(RateLimitViolation violation)
        {
            var key = GetViolationKey(violation.Identifier);
            violation.Id = Guid.NewGuid().ToString();
            violation.RecordedAt = DateTime.UtcNow;
            var json = JsonSerializer.Serialize(violation);

            // Push to list (left push) and trim to last 1000
            await _db.ListLeftPushAsync(key, json);
            await _db.ListTrimAsync(key, 0, 999);

            // Optionally set TTL
            await _db.KeyExpireAsync(key, TimeSpan.FromDays(30));
            _logger.LogWarning("Recorded violation for {Identifier}", violation.Identifier);
        }

        public async Task<List<RateLimitViolation>> GetRateLimitViolationsAsync(string tenantId, DateTime start, DateTime end)
        {
            var results = new List<RateLimitViolation>();
            var server = GetServer();
            var pattern = $"{tenantId}|violations*";

            foreach (var key in server.Keys(pattern: pattern))
            {
                var list = await _db.ListRangeAsync(key);
                foreach (var entry in list)
                {
                    try
                    {
                        var json = entry.ToString();
                        if (string.IsNullOrEmpty(json)) continue;
                        var v = JsonSerializer.Deserialize<RateLimitViolation>(json);
                        if (v != null && v.ViolatedAt >= start && v.ViolatedAt <= end) results.Add(v);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to deserialize violation entry for key {Key}", key);
                    }
                }
            }

            return results.OrderByDescending(v => v.ViolatedAt).ToList();
        }

        // -----------------------------
        // Reset and cleanup
        // -----------------------------
        public async Task ResetRateLimitAsync(string identifier, RateLimitType type)
        {
            var key = GetStateKey(identifier, type);
            await _db.KeyDeleteAsync(key);
            _logger.LogInformation("Reset rate limit for {Identifier} ({Item})", identifier, type);
        }

        public async Task CleanupOldRateLimitsAsync(DateTime olderThan)
        {
            // For Redis, we can iterate keys and remove entries older than 'olderThan' from each sorted set.
            var server = GetServer();
            var pattern = "*|state|*";
            var olderTicks = new DateTimeOffset(olderThan).ToUnixTimeMilliseconds();

            foreach (var key in server.Keys(pattern: pattern))
            {
                await _db.SortedSetRemoveRangeByScoreAsync(key, double.NegativeInfinity, olderTicks - 1);
                // If empty, delete key
                var len = await _db.SortedSetLengthAsync(key);
                if (len == 0) await _db.KeyDeleteAsync(key);
            }

            _logger.LogInformation("Cleaned up rate limits older than {OlderThan}", olderThan);
        }

        // -----------------------------
        // Basic rate limiting operations
        // -----------------------------
        public async Task<bool> AllowRequestAsync(string key, int weight = 1)
        {
            try
            {
                var remaining = await GetRemainingRequestsAsync(key);
                return remaining >= weight;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AllowRequestAsync failed for key {Key}", key);
                return false;
            }
        }

        public async Task<int> GetRemainingRequestsAsync(string key)
        {
            try
            {
                var result = await CheckRateLimitAsync(key, RateLimitType.Request);
                return result.Remaining;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRemainingRequestsAsync failed for key {Key}", key);
                return 0;
            }
        }

        public async Task<DateTime> GetResetTimeAsync(string key)
        {
            try
            {
                var result = await CheckRateLimitAsync(key, RateLimitType.Request);
                return result.ResetTime;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetResetTimeAsync failed for key {Key}", key);
                return DateTime.UtcNow.AddHours(1);
            }
        }

        public async Task RecordRequestAsync(string key, int weight = 1)
        {
            try
            {
                var now = DateTime.UtcNow;
                var zsetKey = GetStateKey(key, RateLimitType.Request);
                var nowTicks = new DateTimeOffset(now).ToUnixTimeMilliseconds();

                // Add 'weight' entries with slightly different member values to avoid collisions
                for (int i = 0; i < weight; i++)
                {
                    var member = $"{nowTicks}-{Guid.NewGuid():N}";
                    await _db.SortedSetAddAsync(zsetKey, member, nowTicks);
                }

                // Optionally set TTL
                await _db.KeyExpireAsync(zsetKey, _defaultWindow.Add(TimeSpan.FromMinutes(5)));
                _logger.LogDebug("Recorded {Weight} requests for {Key}", weight, key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RecordRequestAsync failed for key {Key}", key);
            }
        }

        // -----------------------------
        // Helpers
        // -----------------------------
        private string GetIdentifier(ArbitrationContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (!string.IsNullOrEmpty(context.ProjectId)) return $"{context.TenantId}|{context.ProjectId}";
            if (!string.IsNullOrEmpty(context.UserId)) return $"{context.TenantId}|{context.UserId}";
            return context.TenantId;
        }

        private string GetConfigKey(string identifier, RateLimitType type) => $"{identifier}|config|{type}";
        private string GetStateKey(string identifier, RateLimitType type) => $"{identifier}|state|{type}";
        private string GetViolationKey(string identifier) => $"{identifier}|violations";

        private DateTime GetWindowStart(DateTime now, TimeSpan window)
        {
            if (window.TotalSeconds <= 0) return now;
            var windowSeconds = (long)window.TotalSeconds;
            var currentSeconds = new DateTimeOffset(now).ToUnixTimeSeconds();
            var windowStartSeconds = currentSeconds - (currentSeconds % windowSeconds);
            return DateTimeOffset.FromUnixTimeSeconds(windowStartSeconds).UtcDateTime;
        }

        private IServer GetServer()
        {
            // Get a server to enumerate keys. For clustered environments, pick a single endpoint.
            var multiplexer = _db.Multiplexer;
            var endpoint = multiplexer.GetEndPoints().First();
            return multiplexer.GetServer(endpoint);
        }
    }
}
    //public class RateLimiter : IRateLimiter
    //{
    //    private readonly ILogger<RateLimiter> _logger;

    //    // In-memory stores (in production, use distributed cache like Redis)
    //    private readonly ConcurrentDictionary<string, RateLimitState> _rateLimits = new();
    //    private readonly ConcurrentDictionary<string, RateLimitConfiguration> _configurations = new();
    //    private readonly ConcurrentDictionary<string, List<RateLimitViolation>> _violations = new();

    //    // Default configurations
    //    private const int DefaultRequestLimit = 100;
    //    private const int DefaultTokenLimit = 1000;
    //    private static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(1);

    //    public RateLimiter(ILogger<RateLimiter> logger)
    //    {
    //        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    //    }

    //    #region Interface Implementation

    //    public async Task<RateLimitResult> CheckRateLimitAsync(ArbitrationContext context)
    //    {
    //        var identifier = GetIdentifier(context);
    //        return await CheckRateLimitAsync(identifier, RateLimitItem.Request);
    //    }

    //    public async Task<RateLimitResult> CheckRateLimitAsync(string identifier, RateLimitItem type)
    //    {
    //        var config = await GetRateLimitConfigurationAsync(identifier, type);
    //        var state = GetOrCreateRateLimitState(identifier, type);
    //        var now = DateTime.UtcNow;

    //        var windowStart = GetWindowStart(now, config.Window);

    //        // Clean up old requests outside the current window
    //        state.Requests.RemoveAll(r => r < windowStart);

    //        var currentCount = state.Requests.Count;
    //        var remaining = config.MaxRequests - currentCount;
    //        var resetTime = windowStart + config.Window;

    //        var result = new RateLimitResult
    //        {
    //            IsAllowed = currentCount < config.MaxRequests,
    //            CurrentCount = currentCount,
    //            MaxCount = config.MaxRequests,
    //            Remaining = Math.Max(0, remaining),
    //            ResetTime = resetTime,
    //            Window = config.Window,
    //            Identifier = identifier,
    //            Item = type
    //        };

    //        if (result.IsAllowed)
    //        {
    //            state.Requests.Add(now);
    //            _logger.LogDebug("Rate limit check passed for {Identifier}: {Current}/{Max}",
    //                identifier, currentCount + 1, config.MaxRequests);
    //        }
    //        else
    //        {
    //            _logger.LogWarning("Rate limit exceeded for {Identifier}: {Current}/{Max}",
    //                identifier, currentCount, config.MaxRequests);

    //            await RecordRateLimitViolationAsync(new RateLimitViolation
    //            {
    //                Identifier = identifier,
    //                Item = type,
    //                CurrentCount = currentCount,
    //                MaxCount = config.MaxRequests,
    //                ViolatedAt = now,
    //                ResetTime = resetTime
    //            });
    //        }

    //        return result;
    //    }

    //    public async Task<RateLimitConfiguration> GetRateLimitConfigurationAsync(string identifier, RateLimitItem type)
    //    {
    //        var key = GetConfigKey(identifier, type);

    //        if (_configurations.TryGetValue(key, out var config))
    //        {
    //            return config;
    //        }

    //        // Return default configuration based on type
    //        return new RateLimitConfiguration
    //        {
    //            MaxRequests = type == RateLimitItem.Request ? DefaultRequestLimit : DefaultTokenLimit,
    //            Window = DefaultWindow,
    //            Item = type,
    //            Identifier = identifier,
    //            CreatedAt = DateTime.UtcNow,
    //            UpdatedAt = DateTime.UtcNow
    //        };
    //    }

    //    public async Task UpdateRateLimitConfigurationAsync(string identifier, RateLimitConfiguration configuration)
    //    {
    //        var key = GetConfigKey(identifier, configuration.Item);
    //        configuration.UpdatedAt = DateTime.UtcNow;
    //        _configurations[key] = configuration;

    //        _logger.LogInformation("Updated rate limit configuration for {Identifier}: {MaxRequests}/{Window}",
    //            identifier, configuration.MaxRequests, configuration.Window);
    //    }

    //    public async Task<RateLimitUsage> GetRateLimitUsageAsync(string identifier, RateLimitItem type)
    //    {
    //        var config = await GetRateLimitConfigurationAsync(identifier, type);
    //        var state = GetOrCreateRateLimitState(identifier, type);

    //        var now = DateTime.UtcNow;
    //        var windowStart = GetWindowStart(now, config.Window);

    //        // Clean up old requests
    //        state.Requests.RemoveAll(r => r < windowStart);

    //        return new RateLimitUsage
    //        {
    //            Identifier = identifier,
    //            Item = type,
    //            CurrentCount = state.Requests.Count,
    //            MaxCount = config.MaxRequests,
    //            WindowStart = windowStart,
    //            WindowEnd = windowStart + config.Window,
    //            PercentageUsed = (decimal)state.Requests.Count / config.MaxRequests * 100,
    //            ResetTime = windowStart + config.Window,
    //            Remaining = config.MaxRequests - state.Requests.Count
    //        };
    //    }

    //    public async Task<List<RateLimitUsage>> GetRateLimitUsagesAsync(string tenantId, DateTime start, DateTime end)
    //    {
    //        var usages = new List<RateLimitUsage>();
    //        var tenantKeys = _rateLimits.Keys
    //            .Where(k => k.StartsWith($"{tenantId}|"))
    //            .ToList();

    //        foreach (var key in tenantKeys)
    //        {
    //            var parts = key.Split('|');
    //            if (parts.Length < 3) continue;

    //            var identifier = parts[0];
    //            var type = Enum.Parse<RateLimitItem>(parts[1]);

    //            var usage = await GetRateLimitUsageAsync(identifier, type);
    //            usages.Add(usage);
    //        }

    //        return usages;
    //    }

    //    public async Task<RateLimitQuota> GetRateLimitQuotaAsync(string identifier)
    //    {
    //        var requestConfig = await GetRateLimitConfigurationAsync(identifier, RateLimitItem.Request);
    //        var tokenConfig = await GetRateLimitConfigurationAsync(identifier, RateLimitItem.Token);

    //        return new RateLimitQuota
    //        {
    //            Identifier = identifier,
    //            RequestLimit = requestConfig.MaxRequests,
    //            RequestWindow = requestConfig.Window,
    //            TokenLimit = tokenConfig.MaxRequests,
    //            TokenWindow = tokenConfig.Window,
    //            UpdatedAt = DateTime.UtcNow,
    //            IsActive = true
    //        };
    //    }

    //    public async Task UpdateRateLimitQuotaAsync(string identifier, RateLimitQuota quota)
    //    {
    //        // Update request rate limit
    //        await UpdateRateLimitConfigurationAsync(identifier, new RateLimitConfiguration
    //        {
    //            MaxRequests = quota.RequestLimit,
    //            Window = quota.RequestWindow,
    //            Item = RateLimitItem.Request,
    //            Identifier = identifier
    //        });

    //        // Update token rate limit
    //        await UpdateRateLimitConfigurationAsync(identifier, new RateLimitConfiguration
    //        {
    //            MaxRequests = quota.TokenLimit,
    //            Window = quota.TokenWindow,
    //            Item = RateLimitItem.Token,
    //            Identifier = identifier
    //        });

    //        _logger.LogInformation("Updated rate limit quota for {Identifier}: {RequestLimit}/{RequestWindow}, {TokenLimit}/{TokenWindow}",
    //            identifier, quota.RequestLimit, quota.RequestWindow, quota.TokenLimit, quota.TokenWindow);
    //    }

    //    public async Task RecordRateLimitViolationAsync(RateLimitViolation violation)
    //    {
    //        var key = GetViolationKey(violation.Identifier);

    //        if (!_violations.ContainsKey(key))
    //        {
    //            _violations[key] = new List<RateLimitViolation>();
    //        }

    //        violation.Id = Guid.NewGuid().ToString();
    //        violation.RecordedAt = DateTime.UtcNow;

    //        _violations[key].Add(violation);

    //        // Keep only last 1000 violations per identifier
    //        if (_violations[key].Count > 1000)
    //        {
    //            _violations[key].RemoveAt(0);
    //        }

    //        _logger.LogWarning("Rate limit violation recorded for {Identifier}: {Current}/{Max} (Item: {Item})",
    //            violation.Identifier, violation.CurrentCount, violation.MaxCount, violation.Item);
    //    }

    //    public async Task<List<RateLimitViolation>> GetRateLimitViolationsAsync(string tenantId, DateTime start, DateTime end)
    //    {
    //        var allViolations = new List<RateLimitViolation>();

    //        foreach (var kvp in _violations)
    //        {
    //            if (kvp.Key.StartsWith(tenantId))
    //            {
    //                var violationsInPeriod = kvp.Value
    //                    .Where(v => v.ViolatedAt >= start && v.ViolatedAt <= end)
    //                    .ToList();

    //                allViolations.AddRange(violationsInPeriod);
    //            }
    //        }

    //        return allViolations
    //            .OrderByDescending(v => v.ViolatedAt)
    //            .ToList();
    //    }

    //    public async Task ResetRateLimitAsync(string identifier, RateLimitItem type)
    //    {
    //        var key = GetStateKey(identifier, type);
    //        _rateLimits.TryRemove(key, out _);

    //        _logger.LogInformation("Reset rate limit for {Identifier} ({Item})", identifier, type);
    //    }

    //    public async Task CleanupOldRateLimitsAsync(DateTime olderThan)
    //    {
    //        var keysToRemove = new List<string>();

    //        foreach (var kvp in _rateLimits)
    //        {
    //            var state = kvp.Value;
    //            state.Requests.RemoveAll(r => r < olderThan);

    //            if (!state.Requests.Any())
    //            {
    //                keysToRemove.Add(kvp.Key);
    //            }
    //        }

    //        foreach (var key in keysToRemove)
    //        {
    //            _rateLimits.TryRemove(key, out _);
    //        }

    //        _logger.LogInformation("Cleaned up rate limits older than {OlderThan}", olderThan);
    //    }

    //    public async Task<bool> AllowRequestAsync(string key, int weight = 1)
    //    {
    //        var remaining = await GetRemainingRequestsAsync(key);
    //        return remaining >= weight;
    //    }

    //    public async Task<int> GetRemainingRequestsAsync(string key)
    //    {
    //        try
    //        {
    //            var result = await CheckRateLimitAsync(key, RateLimitItem.Request);
    //            return result.Remaining;
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Failed to get remaining requests for key: {Key}", key);
    //            return 0;
    //        }
    //    }

    //    public async Task<DateTime> GetResetTimeAsync(string key)
    //    {
    //        try
    //        {
    //            var result = await CheckRateLimitAsync(key, RateLimitItem.Request);
    //            return result.ResetTime;
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Failed to get reset time for key: {Key}", key);
    //            return DateTime.UtcNow.AddHours(1); // Fallback
    //        }
    //    }

    //    public async Task RecordRequestAsync(string key, int weight = 1)
    //    {
    //        try
    //        {
    //            var state = GetOrCreateRateLimitState(key, RateLimitItem.Request);
    //            var now = DateTime.UtcNow;

    //            for (int i = 0; i < weight; i++)
    //            {
    //                state.Requests.Add(now);
    //            }

    //            _logger.LogDebug("Recorded {Weight} requests for {Key}", weight, key);
    //        }
    //        catch (Exception ex)
    //        {
    //            _logger.LogError(ex, "Failed to record request for key: {Key}, weight: {Weight}", key, weight);
    //        }
    //    }

    //    #endregion

    //    #region Helper Methods

    //    private string GetIdentifier(ArbitrationContext context)
    //    {
    //        if (!string.IsNullOrEmpty(context.ProjectId))
    //        {
    //            return $"{context.TenantId}|{context.ProjectId}";
    //        }

    //        if (!string.IsNullOrEmpty(context.UserId))
    //        {
    //            return $"{context.TenantId}|{context.UserId}";
    //        }

    //        return context.TenantId;
    //    }

    //    private string GetConfigKey(string identifier, RateLimitItem type)
    //    {
    //        return $"{identifier}|config|{type}";
    //    }

    //    private string GetStateKey(string identifier, RateLimitItem type)
    //    {
    //        return $"{identifier}|state|{type}";
    //    }

    //    private string GetViolationKey(string identifier)
    //    {
    //        return $"{identifier}|violations";
    //    }

    //    private RateLimitState GetOrCreateRateLimitState(string identifier, RateLimitItem type)
    //    {
    //        var key = GetStateKey(identifier, type);

    //        return _rateLimits.GetOrAdd(key, _ => new RateLimitState
    //        {
    //            Identifier = identifier,
    //            Item = type,
    //            Requests = new List<DateTime>(),
    //            CreatedAt = DateTime.UtcNow,
    //            UpdatedAt = DateTime.UtcNow
    //        });
    //    }

    //    private DateTime GetWindowStart(DateTime currentTime, TimeSpan window)
    //    {
    //        if (window.TotalSeconds <= 0)
    //            return currentTime;

    //        var windowSeconds = (long)window.TotalSeconds;
    //        var currentSeconds = new DateTimeOffset(currentTime).ToUnixTimeSeconds();
    //        var windowStartSeconds = currentSeconds - (currentSeconds % windowSeconds);

    //        return DateTimeOffset.FromUnixTimeSeconds(windowStartSeconds).UtcDateTime;
    //    }

    //    #endregion

    //    #region Helper Classes

    //    private class RateLimitState
    //    {
    //        public string Identifier { get; set; }
    //        public RateLimitItem Item { get; set; }
    //        public List<DateTime> Requests { get; set; } = new List<DateTime>();
    //        public DateTime CreatedAt { get; set; }
    //        public DateTime UpdatedAt { get; set; }
    //    }

    //    #endregion
    //}
