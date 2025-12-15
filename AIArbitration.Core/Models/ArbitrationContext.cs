using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class ArbitrationContext
    {
        // User & Tenant Context
        public string UserId { get; set; }
        public string TenantId { get; set; }
        public string? ProjectId { get; set; }
        public string? ApiKeyId { get; set; }

        // Task Requirements
        public string TaskType { get; set; } = "general"; // "chat", "completion", "embedding", "vision", "audio"
        public decimal? MaxCost { get; set; }
        public int? MinIntelligenceScore { get; set; }
        public TimeSpan? MaxLatency { get; set; }
        public int? MinContextLength { get; set; }

        // Rate Limiting (Missing Property - Add this)
        public int? MaxRequestsPerMinute { get; set; }

        // User-specific Requirements
        public List<string> AllowedProviders { get; set; } = new();
        public List<string> BlockedProviders { get; set; } = new();
        public List<string> AllowedModels { get; set; } = new();
        public List<string> BlockedModels { get; set; } = new();

        // Compliance Requirements
        public string? RequiredRegion { get; set; }
        public bool RequireDataResidency { get; set; }
        public bool RequireEncryptionAtRest { get; set; }
        public bool RequireAuditTrail { get; set; }

        // Budget Constraints
        public BudgetStatus? BudgetStatus { get; set; }

        public void ApplyUserConstraints(UserConstraints constraints)
        {
            if (constraints.AllowedModels?.Any() == true)
                AllowedModels = constraints.AllowedModels;

            if (constraints.BlockedModels?.Any() == true)
                BlockedModels = constraints.BlockedModels;

            if (constraints.MaxCostPerRequest.HasValue)
                MaxCost = constraints.MaxCostPerRequest.Value;

            if (constraints.MaxRequestsPerMinute.HasValue)
                MaxRequestsPerMinute = constraints.MaxRequestsPerMinute.Value; // Now this property exists
        }

        public int ExpectedInputTokens { get; set; }
        public int ExpectedOutputTokens { get; set; }
        public bool RequiresStreaming { get; set; }
        public bool RequiresFunctionCalling { get; set; }
        public bool RequiresVision { get; set; }
        public bool RequiresAudio { get; set; }
        public bool RequiresEncryption { get; set; }
        public TimeSpan MaxAllowedLatency { get; set; } = TimeSpan.FromSeconds(30);
        public decimal MaxAllowedCost { get; set; }
        public decimal MinRequiredIntelligenceScore { get; set; }
        public List<CapabilityType> RequiredCapabilities { get; set; } = new();
        public Dictionary<string, object> ContextParameters { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public decimal? EstimatedCost { get; set; }
        public int? EstimatedInputTokens { get; set; }
        public int? EstimatedOutputTokens { get; set; }
        public string SelectionStrategy { get; set; } = GetSelectionStrategy();
        public bool EnableFallback { get; set; }
        public int? MaxFallbackAttempts { get; set; }

        private static string GetSelectionStrategy()
        {
            var context = new ArbitrationContext();
            if (context.MaxCost.HasValue && context.MaxCost < 0.10m)
                return "cost_optimized";

            if (context.MinIntelligenceScore.HasValue && context.MinIntelligenceScore > 70)
                return "intelligence_optimized";

            if (context.MaxLatency.HasValue && context.MaxLatency < TimeSpan.FromSeconds(2))
                return "latency_optimized";

            if (context.RequiredCapabilities.Any())
                return "capability_optimized";

            return "balanced";
        }
    }
}