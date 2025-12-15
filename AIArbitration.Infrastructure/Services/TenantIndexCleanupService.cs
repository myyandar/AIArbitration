using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using AIArbitration.Core.Entities.Enums;
{
    
}

namespace AIArbitration.Infrastructure.Services
{
    public class TenantIndexCleanupService : BackgroundService
    {
        private readonly IConnectionMultiplexer _mux;
        private readonly StackExchange.Redis.IDatabase _db;
        private readonly ILogger<TenantIndexCleanupService> _logger;

        /// <summary>
        /// How often to run the prune job (default: 1 hour).
        /// </summary>
        public TimeSpan RunInterval { get; set; } = TimeSpan.FromHours(1);

        /// <summary>
        /// When backfilling, how many keys to process per batch (tune for your environment).
        /// </summary>
        public int ScanPageSize { get; set; } = 1000;

        public TenantIndexCleanupService(IConnectionMultiplexer mux, ILogger<TenantIndexCleanupService> logger)
        {
            _mux = mux ?? throw new ArgumentNullException(nameof(mux));
            _db = _mux.GetDatabase();
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("TenantIndexCleanupService starting. Interval: {Interval}", RunInterval);

            // Optionally run an initial backfill once at startup
            try
            {
                await BackfillIndexAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Initial backfill failed");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await PruneOrphanedIdentifiersAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Prune job failed");
                }

                await Task.Delay(RunInterval, stoppingToken);
            }

            _logger.LogInformation("TenantIndexCleanupService stopping.");
        }

        /// <summary>
        /// Backfill tenant index sets by scanning existing keys (state/config/violations).
        /// Run once at startup or on demand.
        /// </summary>
        public async Task BackfillIndexAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting tenant index backfill (scan keys).");

            var server = GetServer();
            // Patterns for keys we care about. Adjust to your key naming convention.
            var patterns = new[] { "*|state|*", "*|config|*", "*|violations*" };

            foreach (var pattern in patterns)
            {
                var enumerator = server.Keys(pattern: pattern, pageSize: ScanPageSize).GetEnumerator();
                var batch = new List<string>(ScanPageSize);

                while (!cancellationToken.IsCancellationRequested && enumerator.MoveNext())
                {
                    batch.Add(enumerator.Current.ToString());

                    if (batch.Count >= ScanPageSize)
                    {
                        await ProcessBackfillBatchAsync(batch);
                        batch.Clear();
                    }
                }

                if (batch.Count > 0)
                {
                    await ProcessBackfillBatchAsync(batch);
                    batch.Clear();
                }
            }

            _logger.LogInformation("Tenant index backfill completed.");
        }

        private async Task ProcessBackfillBatchAsync(List<string> keys)
        {
            // Extract identifiers and add to tenant sets
            foreach (var key in keys)
            {
                // Expecting keys like: "{identifier}|state|{type}" or "{identifier}|config|{type}" or "{identifier}|violations"
                var parts = key.Split('|');
                if (parts.Length == 0) continue;

                // identifier is everything up to the first '|' (tenantId|rest) OR if identifier itself contains '|', reconstruct:
                // In our design identifier is the first segment (tenantId) + maybe second; adapt if your identifier contains '|'.
                // Here we assume identifier is parts[0] (tenantId) + optionally parts[1] if you used tenant|id style.
                // Safer approach: if your identifier contains '|' use a different delimiter or store tenant separately.
                var identifier = parts[0];
                // If your identifier format is "tenantId|rest", and keys are "tenantId|rest|state|type", then reconstruct:
                if (parts.Length >= 3)
                {
                    // Reconstruct identifier as everything before the last two segments (state/config/violations and type)
                    var idParts = parts.Take(parts.Length - 2);
                    identifier = string.Join("|", idParts);
                }

                await AddIdentifierToTenantIndexAsync(identifier);
            }
        }

        /// <summary>
        /// Prune identifiers from tenant index sets that have no associated keys.
        /// </summary>
        public async Task PruneOrphanedIdentifiersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting tenant index prune.");

            var server = GetServer();
            // Enumerate tenant index keys: tenant:{tenantId}:identifiers
            var pattern = "tenant:*:identifiers";
            foreach (var key in server.Keys(pattern: pattern, pageSize: ScanPageSize))
            {
                if (cancellationToken.IsCancellationRequested) break;

                var tenantIndexKey = key.ToString();
                var tenantId = ExtractTenantIdFromIndexKey(tenantIndexKey);
                if (string.IsNullOrEmpty(tenantId)) continue;

                var members = await _db.SetMembersAsync(tenantIndexKey);
                if (members == null || members.Length == 0) continue;

                var toRemove = new List<RedisValue>();

                foreach (var member in members)
                {
                    if (cancellationToken.IsCancellationRequested) break;

                    var identifier = (string)member;
                    // Check if any of the related keys exist
                    var stateKeyRequest = GetStateKey(identifier, RateLimitType.Request);
                    var stateKeyToken = GetStateKey(identifier, RateLimitType.Token);
                    var configKeyRequest = GetConfigKey(identifier, RateLimitType.Request);
                    var configKeyToken = GetConfigKey(identifier, RateLimitType.Token);
                    var violationsKey = GetViolationKey(identifier);

                    var exists = await _db.KeyExistsAsync(stateKeyRequest)
                              || await _db.KeyExistsAsync(stateKeyToken)
                              || await _db.KeyExistsAsync(configKeyRequest)
                              || await _db.KeyExistsAsync(configKeyToken)
                              || await _db.KeyExistsAsync(violationsKey);

                    if (!exists)
                    {
                        toRemove.Add(member);
                    }
                }

                if (toRemove.Count > 0)
                {
                    await _db.SetRemoveAsync(tenantIndexKey, toRemove.ToArray());
                    _logger.LogInformation("Pruned {Count} orphaned identifiers from {TenantIndex}", toRemove.Count, tenantIndexKey);
                }
            }

            _logger.LogInformation("Tenant index prune completed.");
        }

        // -------------------------
        // Helper methods (reuse from RedisRateLimiter)
        // -------------------------
        private async Task AddIdentifierToTenantIndexAsync(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return;

            var tenantId = identifier.Split('|').FirstOrDefault();
            if (string.IsNullOrEmpty(tenantId)) return;

            var key = GetTenantIndexKey(tenantId);
            try
            {
                await _db.SetAddAsync(key, identifier);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add identifier {Identifier} to tenant index {Tenant}", identifier, tenantId);
            }
        }

        private string GetTenantIndexKey(string tenantId) => $"tenant:{tenantId}:identifiers";

        private string GetConfigKey(string identifier, RateLimitType type) => $"{identifier}|config|{type}";
        private string GetStateKey(string identifier, RateLimitType type) => $"{identifier}|state|{type}";
        private string GetViolationKey(string identifier) => $"{identifier}|violations";

        private string ExtractTenantIdFromIndexKey(string indexKey)
        {
            // indexKey format: tenant:{tenantId}:identifiers
            var parts = indexKey.Split(':');
            if (parts.Length >= 3) return parts[1];
            return null;
        }

        private IServer GetServer()
        {
            var endpoint = _mux.GetEndPoints().First();
            return _mux.GetServer(endpoint);
        }
    }
}
