using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ModelProvider
    {
        public HealthStatus healthStatus;
        public List<string> supportedModels;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty; // "openai", "anthropic", etc.
        public string DisplayName { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string? DocumentationUrl { get; set; }

        // Authentication
        public AuthenticationType AuthType { get; set; }
        public string? DefaultAuthMethod { get; set; }

        // Provider ModelCapabilities
        public bool SupportsStreaming { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsAudio { get; set; }
        public bool SupportsEmbeddings { get; set; }
        public bool SupportsFineTuning { get; set; }

        // Configuration
        public string? SupportedRegions { get; set; } // JSON array
        public string? DefaultParameters { get; set; } // JSON configuration
        public string? PricingModel { get; set; } // JSON pricing structure
        public string? RateLimits { get; set; } // JSON rate limits

        // Status
        public bool IsEnabled { get; set; } = true;
        public bool IsVerified { get; set; }
        public ProviderTier Tier { get; set; }
        public DateTime VerifiedAt { get; set; }

        // Tracking
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastSyncAt { get; set; }
        public DateTime? LastHealthCheckAt { get; set; }

        // Navigation properties
        public virtual ProviderConfiguration? Configuration { get; set; }
        public virtual ICollection<AIModel> Models { get; set; } = new List<AIModel>();
        public virtual ICollection<ProviderHealth> HealthMetrics { get; set; } = new List<ProviderHealth>();
        public virtual ICollection<ProviderIncident> Incidents { get; set; } = new List<ProviderIncident>();
        public ProviderHealthStatus LastHealthStatus { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public HealthStatus HealthStatus { get; set; }
    }
}
