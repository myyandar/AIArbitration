using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ProviderConfiguration
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProviderId { get; set; } = string.Empty;

        // Authentication Configuration
        public AuthenticationType AuthType { get; set; }
        public string? ApiKey { get; set; } // Encrypted in database
        public string? SecretKey { get; set; } // Encrypted in database
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; } // Encrypted
        public string? TokenEndpoint { get; set; }
        public string? AuthEndpoint { get; set; }
        public string? Scope { get; set; }

        // Headers Configuration
        public string? CustomHeaderName { get; set; }
        public string? CustomHeaderValue { get; set; }

        // Rate Limiting Configuration
        public int RequestsPerMinute { get; set; } = 60;
        public int RequestsPerDay { get; set; } = 10000;
        public int ConcurrentRequests { get; set; } = 10;

        // Retry Configuration
        public int MaxRetries { get; set; } = 3;
        public int RetryDelayMs { get; set; } = 1000;
        public bool ExponentialBackoff { get; set; } = true;

        // Timeout Configuration
        public int TimeoutSeconds { get; set; } = 30;
        public int ConnectionTimeoutSeconds { get; set; } = 10;

        // Proxy Configuration
        public string? ProxyUrl { get; set; }
        public bool UseProxy { get; set; }

        // Cache Configuration
        public bool EnableCaching { get; set; } = true;
        public int CacheDurationSeconds { get; set; } = 300;

        // Circuit Breaker Configuration
        public int CircuitBreakerFailureThreshold { get; set; } = 5;
        public int CircuitBreakerResetSeconds { get; set; } = 60;

        // Regional Configuration
        public string? DefaultRegion { get; set; }
        public string? EndpointUrl { get; set; }

        // Monitoring Configuration
        public bool EnableHealthChecks { get; set; } = true;
        public int HealthCheckIntervalSeconds { get; set; } = 60;

        // Encryption
        public string? EncryptionKey { get; set; } // For encrypting sensitive data

        // Navigation
        public virtual ModelProvider Provider { get; set; } = null!;
        public string? ApiSecret { get; set; }
        public string? AccessToken { get; set; }
        public DateTime? TokenExpiresAt { get; set; }

        // Connection settings
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);
        public bool UseCompression { get; set; } = true;

        // Rate limiting
        public bool EnableRateLimiting { get; set; } = true;
        public int TokensPerMinute { get; set; } = 100000;

        // Circuit breaker
        public bool EnableCircuitBreaker { get; set; } = true;
        public int FailureThreshold { get; set; } = 5;
        public TimeSpan ResetTimeout { get; set; } = TimeSpan.FromMinutes(1);

        // Cost settings
        public decimal ServiceFeePercentage { get; set; } = 0.05m; // 5%
        public decimal DefaultInputTokenPrice { get; set; } = 0.001m; // $0.001 per 1K tokens
        public decimal DefaultOutputTokenPrice { get; set; } = 0.002m; // $0.002 per 1K tokens

        // Default parameters
        public int DefaultMaxTokens { get; set; } = 1024;
        public decimal DefaultTemperature { get; set; } = 0.7m;
        public decimal DefaultTopP { get; set; } = 1.0m;
        public decimal DefaultFrequencyPenalty { get; set; } = 0.0m;
        public decimal DefaultPresencePenalty { get; set; } = 0.0m;

        // Advanced settings
        public string? CustomHeaders { get; set; } // JSON string of custom headers
        public bool EnableLogging { get; set; } = true;
        public string? Region { get; set; }
        public string? Version { get; set; } = "v1";
        public string? Organization { get; set; }
        public string? Project { get; set; }

        // Metadata
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string ConfigurationJson { get; set; } = string.Empty;
        public string BaseUrl { get; set; }
    }
}
