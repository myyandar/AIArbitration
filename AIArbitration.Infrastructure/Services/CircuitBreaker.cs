using AIArbitration.Core;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIArbitration.Core.Entities
{
    /// <summary>
    /// Implementation of circuit breaker pattern
    /// </summary>
    public class CircuitBreakerService : ICircuitBreaker, IDisposable
    {
        private readonly AIArbitrationDbContext _dbContext;
        private readonly ILogger<CircuitBreakerService> _logger;
        private readonly CircuitBreakerOptions _options;
        private readonly SemaphoreSlim _circuitLock = new SemaphoreSlim(1, 1);

        // Use ConcurrentDictionary for thread-safe operations
        private readonly ConcurrentDictionary<string, CircuitBreakerConfig> _circuitConfigs = new();
        private readonly ConcurrentDictionary<string, CircuitState> _circuitStates = new();
        private readonly ConcurrentDictionary<string, CircuitBreakerStats> _circuitStats = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastAccess = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _circuitLocks = new();
        private readonly SemaphoreSlim _globalCircuitLock = new SemaphoreSlim(1, 1);

        private bool _disposed = false;
        private Timer _cleanupTimer;

        public CircuitBreakerService(
            AIArbitrationDbContext dbContext,
            ILogger<CircuitBreakerService> logger,
            IOptions<CircuitBreakerOptions> options,
            ConcurrentDictionary<string, DateTime> lastAccess,
            Timer cleanupTimer)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _lastAccess = lastAccess;
            // Setup cleanup timer (every 5 minutes)
            _cleanupTimer = new Timer(CleanupOldEntries, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
        }

        private void CleanupOldEntries(object state)
        {
            try
            {
                var cutoff = DateTime.UtcNow - TimeSpan.FromMinutes(30);

                // Get all circuit IDs to check
                var circuitIds = _lastAccess.Keys.ToList();

                foreach (var circuitId in circuitIds)
                {
                    if (_lastAccess.TryGetValue(circuitId, out var lastAccessTime) && lastAccessTime < cutoff)
                    {
                        _circuitStates.TryRemove(circuitId, out _);
                        _circuitStats.TryRemove(circuitId, out _);
                        _circuitConfigs.TryRemove(circuitId, out _);
                        _lastAccess.TryRemove(circuitId, out _);

                        if (_circuitLocks.TryRemove(circuitId, out var lockObj))
                        {
                            lockObj?.Dispose();
                        }

                        _logger.LogDebug("Cleaned up old circuit: {CircuitId}", circuitId);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during circuit cleanup");
            }
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _globalCircuitLock?.Dispose();
                    _cleanupTimer?.Dispose();

                    foreach (var lockObj in _circuitLocks.Values)
                    {
                        lockObj?.Dispose();
                    }

                    _circuitLocks.Clear();
                }
                _disposed = true;
            }
        }

        public async Task<CircuitState> GetCircuitStateAsync(string circuitId)
        {
            // Update last access time
            UpdateLastAccess(circuitId);
            ValidateCircuitId(circuitId);


            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await _circuitLock.WaitAsync();

                // Check memory cache first
                if (_circuitStates.TryGetValue(circuitId, out var cachedState))
                {
                    // Check if we need to transition from OPEN to HALF_OPEN based on timeout
                    if (cachedState == CircuitState.Open)
                    {
                        if (await GetCircuitAsync(circuitId) != null && (await GetCircuitAsync(circuitId)).LastStateChange.HasValue)
                        {
                            var timeout = (await GetCircuitAsync(circuitId)).Config?.ResetTimeout ?? _options.DefaultResetTimeout;
                            if (DateTime.UtcNow - (await GetCircuitAsync(circuitId)).LastStateChange.Value > timeout)
                            {
                                await TransitionToHalfOpenAsync(circuitId);
                                return CircuitState.HalfOpen;
                            }
                        }
                    }
                    return cachedState;
                }

                // Load from database
                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                {
                    // Create default circuit
                    circuit = await CreateDefaultCircuitAsync(circuitId);
                }

                _circuitStates[circuitId] = circuit.CurrentState;
                return circuit.CurrentState;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting circuit state for Circuit: {circuitId}", circuitId);
                return CircuitState.Closed; // Fail-safe: allow requests by default
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task<bool> IsCircuitClosedAsync(string circuitId)
        {
            var state = await GetCircuitStateAsync(circuitId);
            return state == CircuitState.Closed;
        }

        public async Task<bool> IsCircuitOpenAsync(string circuitId)
        {
            var state = await GetCircuitStateAsync(circuitId);
            return state == CircuitState.Open;
        }

        public async Task<bool> IsCircuitHalfOpenAsync(string circuitId)
        {
            var state = await GetCircuitStateAsync(circuitId);
            return state == CircuitState.HalfOpen;
        }

        public async Task<bool> AllowRequestAsync(string circuitId)
        {
            ValidateCircuitId(circuitId);

            try
            {
                var state = await GetCircuitStateAsync(circuitId);

                if (state == CircuitState.Open)
                {
                    // Check if reset timeout has passed
                    var circuit = await GetCircuitAsync(circuitId);
                    if (circuit != null && circuit.LastStateChange.HasValue)
                    {
                        var timeout = circuit.Config?.ResetTimeout ?? _options.DefaultResetTimeout;
                        if (DateTime.UtcNow - circuit.LastStateChange.Value > timeout)
                        {
                            await TransitionToHalfOpenAsync(circuitId);
                            return true; // Allow a test request
                        }
                    }
                    return false;
                }

                if (state == CircuitState.HalfOpen)
                {
                    // In half-open state, only allow limited number of test requests
                    var circuit = await GetCircuitAsync(circuitId);
                    var testRequests = circuit?.HalfOpenTestRequests ?? 0;
                    var maxTestRequests = circuit?.Config?.MaxHalfOpenTestRequests ?? _options.DefaultMaxHalfOpenTestRequests;

                    return testRequests < maxTestRequests;
                }

                return true; // Circuit is closed
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if request is allowed for Circuit: {CircuitId}", circuitId);
                return true; // Fail-safe: allow requests by default
            }
        }

        public async Task RecordSuccessAsync(string circuitId)
        {
            ValidateCircuitId(circuitId);

            try
            {
                await _circuitLock.WaitAsync();

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                    return;

                var now = DateTime.UtcNow;
                var config = await GetCircuitConfigurationAsync(circuitId);

                // Update statistics
                circuit.SuccessCount++;
                circuit.TotalRequests++;
                circuit.LastSuccessTime = now;
                circuit.LastUpdated = now;

                // Update sliding window
                await UpdateSlidingWindowAsync(circuitId, true, now);

                // Check if we should transition from half-open to closed
                if (circuit.CurrentState == CircuitState.HalfOpen)
                {
                    circuit.HalfOpenTestRequests++;

                    // If we've had enough successful test requests, close the circuit
                    if (circuit.HalfOpenTestRequests >= config.SuccessThreshold)
                    {
                        await CloseCircuitAsync(circuitId);
                        await RecordEventAsync(circuitId, CircuitEventType.Closed, "Circuit closed after successful test requests");
                    }
                }
                else if (circuit.CurrentState == CircuitState.Closed)
                {
                    // Reset failure count if we're in a success streak
                    var timeWindow = config.FailureThresholdTimeWindow;
                    var recentFailures = await GetRecentFailuresAsync(circuitId, timeWindow);

                    if (recentFailures == 0)
                    {
                        circuit.ConsecutiveFailures = 0;
                    }
                }

                await SaveCircuitAsync(circuit);
                await UpdateCircuitStatsAsync(circuitId, true);

                _logger.LogDebug($"Success recorded for Circuit: {circuitId}", circuitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording success for Circuit: {circuitId}", circuitId);
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task RecordFailureAsync(string circuitId, Exception exception)
        {
            ValidateCircuitId(circuitId);

            try
            {
                await _circuitLock.WaitAsync();

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                    return;

                var now = DateTime.UtcNow;
                var config = await GetCircuitConfigurationAsync(circuitId);

                // Update statistics
                circuit.FailureCount++;
                circuit.TotalRequests++;
                circuit.ConsecutiveFailures++;
                circuit.LastFailureTime = now;
                circuit.LastFailureException = exception?.Message ?? "Unknown error";
                circuit.LastUpdated = now;

                // Update sliding window
                await UpdateSlidingWindowAsync(circuitId, false, now);

                // Check if we should trip the circuit
                if (circuit.CurrentState == CircuitState.Closed || circuit.CurrentState == CircuitState.HalfOpen)
                {
                    var recentFailures = await GetRecentFailuresAsync(circuitId, config.FailureThresholdTimeWindow);
                    var failureRate = circuit.TotalRequests > 0 ?
                        (decimal)recentFailures / circuit.TotalRequests * 100 : 0;

                    if (recentFailures >= config.FailureThreshold || failureRate >= config.FailurePercentageThreshold)
                    {
                        await TripCircuitAsync(circuitId, exception);
                    }
                }
                else if (circuit.CurrentState == CircuitState.HalfOpen)
                {
                    // If we fail in half-open state, go back to open
                    await TripCircuitAsync(circuitId, exception);
                }

                await SaveCircuitAsync(circuit);
                await UpdateCircuitStatsAsync(circuitId, false);
                await RecordEventAsync(circuitId, CircuitEventType.Failure, exception?.Message ?? "Failure recorded");

                _logger.LogWarning($"Failure recorded for Circuit: {circuitId}, Exception: {exception?.Message}",
                    circuitId, exception?.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error recording failure for Circuit: {circuitId}", circuitId);
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task ResetCircuitAsync(string circuitId)
        {
            ValidateCircuitId(circuitId);

            try
            {
                await _circuitLock.WaitAsync();

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                    return;

                circuit.CurrentState = CircuitState.Closed;
                circuit.ConsecutiveFailures = 0;
                circuit.HalfOpenTestRequests = 0;
                circuit.LastStateChange = DateTime.UtcNow;
                circuit.LastUpdated = DateTime.UtcNow;

                await SaveCircuitAsync(circuit);
                _circuitStates[circuitId] = CircuitState.Closed;

                await RecordEventAsync(circuitId, CircuitEventType.Reset, "Circuit manually reset");

                _logger.LogInformation($"Circuit reset: {circuitId}", circuitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error resetting circuit: {circuitId}", circuitId);
                throw;
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task TripCircuitAsync(string circuitId, Exception exception)
        {
            ValidateCircuitId(circuitId);

            try
            {
                await _circuitLock.WaitAsync();

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                    return;

                circuit.CurrentState = CircuitState.Open;
                circuit.LastStateChange = DateTime.UtcNow;
                circuit.LastUpdated = DateTime.UtcNow;
                circuit.LastTripException = exception?.Message ?? "Circuit tripped";

                await SaveCircuitAsync(circuit);
                _circuitStates[circuitId] = CircuitState.Open;

                await RecordEventAsync(circuitId, CircuitEventType.Opened, exception?.Message ?? "Circuit tripped");

                _logger.LogWarning($"Circuit tripped: {circuitId}, Reason: {exception?.Message}",
                    circuitId, exception?.Message ?? "Threshold exceeded");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error tripping circuit: {circuitId}", circuitId);
                throw;
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task<CircuitBreakerStats> GetCircuitStatsAsync(string circuitId)
        {
            ValidateCircuitId(circuitId);

            try
            {
                // Check memory cache first
                if (_circuitStats.TryGetValue(circuitId, out var cachedStats))
                {
                    return cachedStats;
                }

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                    return CreateDefaultStats(circuitId);

                var now = DateTime.UtcNow;
                var config = await GetCircuitConfigurationAsync(circuitId);
                var timeWindow = config.FailureThresholdTimeWindow;

                // Calculate recent metrics
                var recentFailures = await GetRecentFailuresAsync(circuitId, timeWindow);
                var recentSuccesses = await GetRecentSuccessesAsync(circuitId, timeWindow);
                var recentTotal = recentFailures + recentSuccesses;
                var recentFailureRate = recentTotal > 0 ? (decimal)recentFailures / recentTotal * 100 : 0;

                var stats = new CircuitBreakerStats
                {
                    CircuitId = circuitId,
                    CurrentState = circuit.CurrentState,
                    TotalRequests = circuit.TotalRequests,
                    SuccessCount = circuit.SuccessCount,
                    FailureCount = circuit.FailureCount,
                    ConsecutiveFailures = circuit.ConsecutiveFailures,
                    SuccessRate = circuit.TotalRequests > 0 ?
                        (decimal)circuit.SuccessCount / circuit.TotalRequests * 100 : 0,
                    RecentFailureRate = recentFailureRate,
                    LastSuccessTime = circuit.LastSuccessTime,
                    LastFailureTime = circuit.LastFailureTime,
                    LastStateChange = circuit.LastStateChange,
                    IsHealthy = circuit.CurrentState == CircuitState.Closed,
                    TimeSinceLastStateChange = circuit.LastStateChange.HasValue ?
                        now - circuit.LastStateChange.Value : (TimeSpan?)null,
                    Config = config
                };

                // Cache the stats
                _circuitStats[circuitId] = stats;

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting circuit stats for Circuit: {circuitId}", circuitId);
                return CreateDefaultStats(circuitId);
            }
        }

        public async Task<List<CircuitBreakerEvent>> GetCircuitEventsAsync(string circuitId, DateTime start, DateTime end)
        {
            ValidateCircuitId(circuitId);

            if (start > end)
                throw new ArgumentException($"Start date cannot be after end date", nameof(start));

            try
            {
                return await _dbContext.CircuitBreakerEvents
                    .Where(e => e.CircuitId == circuitId &&
                               e.Timestamp >= start &&
                               e.Timestamp <= end)
                    .OrderByDescending(e => e.Timestamp)
                    .Take(1000) // Limit results
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting circuit events for Circuit: {circuitId}", circuitId);
                throw;
            }
        }

        public async Task<Dictionary<string, CircuitState>> GetAllCircuitStatesAsync()
        {
            try
            {
                var circuits = await _dbContext.CircuitBreakers
                    .Select(c => new { c.CircuitId, c.CurrentState })
                    .ToListAsync();

                return circuits.ToDictionary(c => c.CircuitId, c => c.CurrentState);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting all circuit states");
                throw;
            }
        }

        public async Task<bool> IsHealthyAsync(string circuitId)
        {
            var state = await GetCircuitStateAsync(circuitId);
            return state == CircuitState.Closed;
        }

        public async Task<List<CircuitHealth>> GetCircuitHealthStatusAsync()
        {
            try
            {
                var circuits = await _dbContext.CircuitBreakers
                    .Include(c => c.Config)
                    .ToListAsync();

                var healthStatus = new List<CircuitHealth>();
                var now = DateTime.UtcNow;

                foreach (var circuit in circuits)
                {
                    var config = circuit.Config ?? GetDefaultConfig();
                    var recentFailures = await GetRecentFailuresAsync(circuit.CircuitId, config.FailureThresholdTimeWindow);
                    var recentSuccesses = await GetRecentSuccessesAsync(circuit.CircuitId, config.FailureThresholdTimeWindow);
                    var recentTotal = recentFailures + recentSuccesses;
                    var recentFailureRate = recentTotal > 0 ? (decimal)recentFailures / recentTotal * 100 : 0;

                    healthStatus.Add(new CircuitHealth
                    {
                        CircuitId = circuit.CircuitId,
                        CurrentState = circuit.CurrentState,
                        IsHealthy = circuit.CurrentState == CircuitState.Closed,
                        FailureRate = recentFailureRate,
                        RecentFailures = recentFailures,
                        RecentSuccesses = recentSuccesses,
                        TotalRequests = circuit.TotalRequests,
                        SuccessCount = circuit.SuccessCount,
                        FailureCount = circuit.FailureCount,
                        LastStateChange = circuit.LastStateChange,
                        TimeSinceLastStateChange = circuit.LastStateChange.HasValue ?
                            now - circuit.LastStateChange.Value : (TimeSpan?)null,
                        Config = config
                    });
                }

                return healthStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting circuit health status");
                throw;
            }
        }

        public async Task UpdateCircuitConfigurationAsync(string circuitId, CircuitBreakerConfig config)
        {
            ValidateCircuitId(circuitId);

            if (config == null)
                throw new ArgumentNullException(nameof(config));

            try
            {
                await _circuitLock.WaitAsync();

                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null)
                {
                    circuit = await CreateDefaultCircuitAsync(circuitId);
                }

                // Update or create config
                if (circuit.ConfigId != null)
                {
                    var existingConfig = await _dbContext.CircuitBreakerConfigs
                        .FindAsync(circuit.ConfigId);

                    if (existingConfig != null)
                    {
                        existingConfig.FailureThreshold = config.FailureThreshold;
                        existingConfig.FailurePercentageThreshold = config.FailurePercentageThreshold;
                        existingConfig.FailureThresholdTimeWindow = config.FailureThresholdTimeWindow;
                        existingConfig.ResetTimeout = config.ResetTimeout;
                        existingConfig.MaxHalfOpenTestRequests = config.MaxHalfOpenTestRequests;
                        existingConfig.SuccessThreshold = config.SuccessThreshold;
                        existingConfig.EnableSlidingWindow = config.EnableSlidingWindow;
                        existingConfig.LastUpdated = DateTime.UtcNow;

                        _dbContext.CircuitBreakerConfigs.Update(existingConfig);
                    }
                }
                else
                {
                    config.Id = Guid.NewGuid().ToString();
                    config.CreatedAt = DateTime.UtcNow;
                    config.LastUpdated = DateTime.UtcNow;

                    await _dbContext.CircuitBreakerConfigs.AddAsync(config);
                    circuit.ConfigId = config.Id;
                }

                circuit.LastUpdated = DateTime.UtcNow;
                await SaveCircuitAsync(circuit);
                await _dbContext.SaveChangesAsync();

                // Update cache
                _circuitConfigs[circuitId] = config;

                await RecordEventAsync(circuitId, CircuitEventType.ConfigUpdated, "Circuit configuration updated");

                _logger.LogInformation($"Circuit configuration updated: {circuitId}", circuitId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating circuit configuration for Circuit: {circuitId}", circuitId);
                throw;
            }
            finally
            {
                _circuitLock.Release();
            }
        }

        public async Task<CircuitBreakerConfig> GetCircuitConfigurationAsync(string circuitId)
        {
            ValidateCircuitId(circuitId);

            // Check memory cache first
            if (_circuitConfigs.TryGetValue(circuitId, out var cachedConfig))
            {
                return cachedConfig;
            }

            try
            {
                var circuit = await GetCircuitAsync(circuitId);
                if (circuit == null || circuit.ConfigId == null)
                {
                    return GetDefaultConfig();
                }

                var config = await _dbContext.CircuitBreakerConfigs
                    .FindAsync(circuit.ConfigId);

                if (config == null)
                {
                    return GetDefaultConfig();
                }

                // Cache the config
                _circuitConfigs[circuitId] = config;

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting circuit configuration for Circuit: {circuitId}", circuitId);
                return GetDefaultConfig();
            }
        }

        #region Private Helper Methods

        private async Task<CircuitBreaker?> GetCircuitAsync(string circuitId)
        {
            return await _dbContext.CircuitBreakers
                .Include(c => c.Config)
                .FirstOrDefaultAsync(c => c.CircuitId == circuitId);
        }

        private async Task<CircuitBreaker> CreateDefaultCircuitAsync(string circuitId)
        {
            var circuit = new CircuitBreaker
            {
                Id = Guid.NewGuid().ToString(),
                CircuitId = circuitId,
                CurrentState = CircuitState.Closed,
                TotalRequests = 0,
                SuccessCount = 0,
                FailureCount = 0,
                ConsecutiveFailures = 0,
                HalfOpenTestRequests = 0,
                CreatedAt = DateTime.UtcNow,
                LastUpdated = DateTime.UtcNow
            };

            await _dbContext.CircuitBreakers.AddAsync(circuit);
            await _dbContext.SaveChangesAsync();

            return circuit;
        }

        private async Task SaveCircuitAsync(CircuitBreaker circuit)
        {
            circuit.LastUpdated = DateTime.UtcNow;
            _dbContext.CircuitBreakers.Update(circuit);
            await _dbContext.SaveChangesAsync();
        }

        private async Task CloseCircuitAsync(string circuitId)
        {
            var circuit = await GetCircuitAsync(circuitId);
            if (circuit == null)
                return;

            circuit.CurrentState = CircuitState.Closed;
            circuit.ConsecutiveFailures = 0;
            circuit.HalfOpenTestRequests = 0;
            circuit.LastStateChange = DateTime.UtcNow;

            await SaveCircuitAsync(circuit);
            _circuitStates[circuitId] = CircuitState.Closed;
        }

        private async Task TransitionToHalfOpenAsync(string circuitId)
        {
            var circuit = await GetCircuitAsync(circuitId);
            if (circuit == null)
                return;

            circuit.CurrentState = CircuitState.HalfOpen;
            circuit.HalfOpenTestRequests = 0;
            circuit.LastStateChange = DateTime.UtcNow;

            await SaveCircuitAsync(circuit);
            _circuitStates[circuitId] = CircuitState.HalfOpen;

            await RecordEventAsync(circuitId, CircuitEventType.HalfOpen, $"Circuit transitioned to half-open for testing");
        }

        private async Task UpdateSlidingWindowAsync(string circuitId, bool isSuccess, DateTime timestamp)
        {
            try
            {
                var windowEntry = new CircuitBreakerWindow
                {
                    Id = Guid.NewGuid().ToString(),
                    CircuitId = circuitId,
                    IsSuccess = isSuccess,
                    Timestamp = timestamp,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.CircuitBreakerWindows.AddAsync(windowEntry);

                // Clean up old window entries
                var config = await GetCircuitConfigurationAsync(circuitId);
                var cutoff = timestamp - config.FailureThresholdTimeWindow - TimeSpan.FromMinutes(5); // Add buffer

                var oldEntries = _dbContext.CircuitBreakerWindows
                    .Where(w => w.CircuitId == circuitId && w.Timestamp < cutoff);

                _dbContext.CircuitBreakerWindows.RemoveRange(oldEntries);

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating sliding window for Circuit: {circuitId}", circuitId);
            }
        }

        private async Task<int> GetRecentFailuresAsync(string circuitId, TimeSpan timeWindow)
        {
            var cutoff = DateTime.UtcNow - timeWindow;

            return await _dbContext.CircuitBreakerWindows
                .CountAsync(w => w.CircuitId == circuitId &&
                               !w.IsSuccess &&
                               w.Timestamp >= cutoff);
        }

        private async Task<int> GetRecentSuccessesAsync(string circuitId, TimeSpan timeWindow)
        {
            var cutoff = DateTime.UtcNow - timeWindow;

            return await _dbContext.CircuitBreakerWindows
                .CountAsync(w => w.CircuitId == circuitId &&
                               w.IsSuccess &&
                               w.Timestamp >= cutoff);
        }

        private async Task UpdateCircuitStatsAsync(string circuitId, bool isSuccess)
        {
            try
            {
                var now = DateTime.UtcNow;
                var stats = await _dbContext.CircuitBreakerStatistics
                    .FirstOrDefaultAsync(s => s.CircuitId == circuitId && s.Date == now.Date);

                if (stats == null)
                {
                    stats = new CircuitBreakerStatistics
                    {
                        Id = Guid.NewGuid().ToString(),
                        CircuitId = circuitId,
                        Date = now.Date,
                        CreatedAt = now,
                        LastUpdated = now
                    };
                    await _dbContext.CircuitBreakerStatistics.AddAsync(stats);
                }

                if (isSuccess)
                {
                    stats.SuccessCount++;
                }
                else
                {
                    stats.FailureCount++;
                }
                stats.TotalRequests++;
                stats.LastUpdated = now;

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating circuit statistics for Circuit: {circuitId}", circuitId);
            }
        }

        private async Task RecordEventAsync(string circuitId, CircuitEventType eventType, string details)
        {
            try
            {
                var circuitEvent = new CircuitBreakerEvent
                {
                    Id = Guid.NewGuid().ToString(),
                    CircuitId = circuitId,
                    EventType = eventType,
                    Details = details,
                    Timestamp = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.CircuitBreakerEvents.AddAsync(circuitEvent);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording circuit event for Circuit: {CircuitId}", circuitId);
            }
        }

        private CircuitBreakerConfig GetDefaultConfig()
        {
            return new CircuitBreakerConfig
            {
                FailureThreshold = _options.DefaultFailureThreshold,
                FailurePercentageThreshold = _options.DefaultFailurePercentageThreshold,
                FailureThresholdTimeWindow = _options.DefaultFailureThresholdTimeWindow,
                ResetTimeout = _options.DefaultResetTimeout,
                MaxHalfOpenTestRequests = _options.DefaultMaxHalfOpenTestRequests,
                SuccessThreshold = _options.DefaultSuccessThreshold,
                EnableSlidingWindow = true
            };
        }

        private CircuitBreakerStats CreateDefaultStats(string circuitId)
        {
            return new CircuitBreakerStats
            {
                CircuitId = circuitId,
                CurrentState = CircuitState.Closed,
                TotalRequests = 0,
                SuccessCount = 0,
                FailureCount = 0,
                ConsecutiveFailures = 0,
                SuccessRate = 0,
                RecentFailureRate = 0,
                IsHealthy = true,
                Config = GetDefaultConfig()
            };
        }

        private void ValidateCircuitId(string circuitId)
        {
            if (string.IsNullOrEmpty(circuitId))
                throw new ArgumentException("$Circuit ID cannot be null or empty", nameof(circuitId));
        }

        #endregion

        // Helper method to get or create circuit-specific lock
        private SemaphoreSlim GetOrCreateCircuitLock(string circuitId)
        {
            return _circuitLocks.GetOrAdd(circuitId, _ => new SemaphoreSlim(1, 1));
        }

        // Helper method to update last access time
        private void UpdateLastAccess(string circuitId)
        {
            _lastAccess[circuitId] = DateTime.UtcNow;
        }

        public async Task<ModelResponse> ExecuteAsync(Func<Task<ModelResponse>> action, string circuitId)
        {
            ValidateCircuitId(circuitId);

            var startTime = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Check circuit state
                if (!await AllowRequestAsync(circuitId))
                {
                    stopwatch.Stop();

                    var circuitState = await GetCircuitStateAsync(circuitId);
                    var stats = await GetCircuitStatsAsync(circuitId);

                    _logger.LogWarning(
                        $"Circuit breaker blocked request for {circuitId}. State: {circuitState}, " +
                        $"Failures: {stats.FailureCount}, SuccessRate: {stats.SuccessRate}%",
                        circuitId, circuitState, stats.FailureCount, stats.SuccessRate);

                    return new ModelResponse
                    {
                        IsSuccess = false,
                        ErrorMessage = $"Circuit '{circuitId}' is {circuitState}. Requests are blocked.",
                        Provider = "CircuitBreaker",
                        ModelId = circuitId,
                        ProcessingTime = stopwatch.Elapsed,
                        Metadata = new Dictionary<string, object>
                {
                    { "CircuitState", circuitState.ToString() },
                    { "CircuitId", circuitId },
                    { "TotalFailures", stats.FailureCount },
                    { "SuccessRate", stats.SuccessRate }
                }
                    };
                }

                _logger.LogDebug($"Circuit {circuitId} allowing request execution", circuitId);

                // Execute the action with timeout protection (optional)
                var response = await action();
                stopwatch.Stop();

                // Update circuit based on response
                if (response.IsSuccess)
                {
                    await RecordSuccessAsync(circuitId);
                    _logger.LogDebug($"Circuit {circuitId} recorded success after {stopwatch.ElapsedMilliseconds}ms",
                        circuitId, stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    var exception = response.Exception ?? new Exception(response.ErrorMessage ?? "Unknown error");
                    await RecordFailureAsync(circuitId, exception);

                    _logger.LogWarning($"Circuit {circuitId} recorded failure after {stopwatch.ElapsedMilliseconds}ms: {response.ErrorMessage}",
                        circuitId, stopwatch.ElapsedMilliseconds, response.ErrorMessage);
                }

                return response;
            }
            catch (TimeoutException tex)
            {
                stopwatch.Stop();
                await RecordFailureAsync(circuitId, tex);

                _logger.LogError(tex, $"Circuit {circuitId} timed out after {stopwatch.ElapsedMilliseconds}ms",
                    circuitId, stopwatch.ElapsedMilliseconds);

                return new ModelResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Request timed out after {stopwatch.ElapsedMilliseconds}ms",
                    ProcessingTime = stopwatch.Elapsed,
                    Provider = "CircuitBreaker",
                    ModelId = circuitId,
                    Exception = tex
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await RecordFailureAsync(circuitId, ex);

                _logger.LogError(ex, $"Circuit {circuitId} execution failed after {stopwatch.ElapsedMilliseconds}ms",
                    circuitId, stopwatch.ElapsedMilliseconds);

                return new ModelResponse
                {
                    IsSuccess = false,
                    ErrorMessage = ex.Message,
                    ProcessingTime = stopwatch.Elapsed,
                    Provider = "CircuitBreaker",
                    ModelId = circuitId,
                    Exception = ex
                };
            }
        }
    } 
}

// Update your methods to use circuit-specific locks instead of global lock
//public async Task<CircuitState> GetCircuitStateAsync(string circuitId)
//{
//    ValidateCircuitId(circuitId);

//    // Update last access time
//    UpdateLastAccess(circuitId);

//    try
//    {
//        var circuitLock = GetOrCreateCircuitLock(circuitId);
//        await circuitLock.WaitAsync();

//        // ... rest of your existing GetCircuitStateAsync implementation ...
//    }
//    finally
//    {
//        circuitLock?.Release();
//    }
//}

// Implement IDisposable
//public void Dispose()
//{
//    Dispose(true);
//    GC.SuppressFinalize(this);
//}

//protected virtual void Dispose(bool disposing)
//{
//    if (!_disposed)
//    {
//        if (disposing)
//        {
//            _globalCircuitLock?.Dispose();
//            _cleanupTimer?.Dispose();

//            foreach (var lockObj in _circuitLocks.Values)
//            {
//                lockObj?.Dispose();
//            }

//            _circuitLocks.Clear();
//        }
//        _disposed = true;
//    }
//}
//public async Task<CircuitState> GetCircuitStateAsync(string circuitId)
//{
//    // Update last access time
//    UpdateLastAccess(circuitId);
//    ValidateCircuitId(circuitId);

//    var startTime = DateTime.UtcNow;
//    var stopwatch = Stopwatch.StartNew();

//    try
//    {

//        // Check circuit state
//        if (!await AllowRequestAsync(circuitId))
//        {
//            stopwatch.Stop();

//            var circuitLock = GetOrCreateCircuitLock(circuitId);
//            await circuitLock.WaitAsync();
//            var circuitState = await GetCircuitStateAsync(circuitId);
//            var stats = await GetCircuitStatsAsync(circuitId);

//            _logger.LogWarning(
//                $"Circuit breaker blocked request for {circuitId}. State: {circuitState}, " +
//                $"Failures: {stats.FailureCount}, SuccessRate: {stats.SuccessRate}%",
//                circuitId, circuitState, stats.FailureCount, stats.SuccessRate);

//            return new ModelResponse
//            {
//                IsSuccess = false,
//                ErrorMessage = $"Circuit '{circuitId}' is {circuitState}. Requests are blocked.",
//                Provider = "CircuitBreaker",
//                ModelId = circuitId,
//                ProcessingTime = stopwatch.Elapsed,
//                Metadata = new Dictionary<string, object>
//        {
//            { "CircuitState", circuitState.ToString() },
//            { "CircuitId", circuitId },
//            { "TotalFailures", stats.FailureCount },
//            { "SuccessRate", stats.SuccessRate }
//        }
//            };
//        }

//        _logger.LogDebug($"Circuit {circuitId} allowing request execution", circuitId);

//        // Execute the action with timeout protection (optional)
//        var response = await action();
//        stopwatch.Stop();

//        // Update circuit based on response
//        if (response.IsSuccess)
//        {
//            await RecordSuccessAsync(circuitId);
//            _logger.LogDebug($"Circuit {circuitId} recorded success after {stopwatch.ElapsedMilliseconds}ms",
//                circuitId, stopwatch.ElapsedMilliseconds);
//        }
//        else
//        {
//            var exception = response.Exception ?? new Exception(response.ErrorMessage ?? "Unknown error");
//            await RecordFailureAsync(circuitId, exception);

//            _logger.LogWarning($"Circuit {circuitId} recorded failure after {stopwatch.ElapsedMilliseconds}ms: {response.ErrorMessage}",
//                circuitId, stopwatch.ElapsedMilliseconds, response.ErrorMessage);
//        }

//        return response;
//    }
//    catch (TimeoutException tex)
//    {
//        stopwatch.Stop();
//        await RecordFailureAsync(circuitId, tex);

//        _logger.LogError(tex, $"Circuit {circuitId} timed out after {stopwatch.ElapsedMilliseconds}ms",
//            circuitId, stopwatch.ElapsedMilliseconds);

//        return new ModelResponse
//        {
//            IsSuccess = false,
//            ErrorMessage = $"Request timed out after {stopwatch.ElapsedMilliseconds}ms",
//            ProcessingTime = stopwatch.Elapsed,
//            Provider = "CircuitBreaker",
//            ModelId = circuitId,
//            Exception = tex
//        };
//    }
//    catch (Exception ex)
//    {
//        stopwatch.Stop();
//        await RecordFailureAsync(circuitId, ex);

//        _logger.LogError(ex, "Circuit {CircuitId} execution failed after {ElapsedMs}ms",
//            circuitId, stopwatch.ElapsedMilliseconds);

//        return new ModelResponse
//        {
//            IsSuccess = false,
//            ErrorMessage = ex.Message,
//            ProcessingTime = stopwatch.Elapsed,
//            Provider = "CircuitBreaker",
//            ModelId = circuitId,
//            Exception = ex
//        };
//    }
//}

/// <summary>
/// ///////////////////////////////////////////////////////////////////
/// </summary>
/// <param name="state"></param>

// ... rest of your existing methods ...

