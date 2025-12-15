using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    /// <summary>
    /// Circuit breaker pattern implementation for handling faults when calling external services
    /// </summary>
    public interface ICircuitBreaker
    {
        // Circuit state management
        Task<CircuitState> GetCircuitStateAsync(string circuitId);
        Task<bool> IsCircuitClosedAsync(string circuitId);
        Task<bool> IsCircuitOpenAsync(string circuitId);
        Task<bool> IsCircuitHalfOpenAsync(string circuitId);

        // Circuit operations
        Task<bool> AllowRequestAsync(string circuitId);
        Task RecordSuccessAsync(string circuitId);
        Task RecordFailureAsync(string circuitId, Exception exception);
        Task ResetCircuitAsync(string circuitId);
        Task TripCircuitAsync(string circuitId, Exception exception);

        // Monitoring and statistics
        Task<CircuitBreakerStats> GetCircuitStatsAsync(string circuitId);
        Task<List<CircuitBreakerEvent>> GetCircuitEventsAsync(string circuitId, DateTime start, DateTime end);
        Task<Dictionary<string, CircuitState>> GetAllCircuitStatesAsync();

        // Health checks
        Task<bool> IsHealthyAsync(string circuitId);
        Task<List<CircuitHealth>> GetCircuitHealthStatusAsync();

        // Configuration
        Task UpdateCircuitConfigurationAsync(string circuitId, CircuitBreakerConfig config);
        Task<CircuitBreakerConfig> GetCircuitConfigurationAsync(string circuitId);
        Task<ModelResponse> ExecuteAsync(Func<Task<ModelResponse>> value, string name);
    }
}
