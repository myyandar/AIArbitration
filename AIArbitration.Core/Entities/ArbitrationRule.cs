using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ArbitrationRule
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Rule Identification
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string RuleType { get; set; } = "model_selection"; // "model_selection", "cost_optimization", "compliance", "performance"

        // Ownership & Scope
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? CreatedBy { get; set; }
        public string? ModifiedBy { get; set; }

        // Priority & Ordering
        public int Priority { get; set; } = 100; // Lower number = higher priority
        public int ExecutionOrder { get; set; } = 1;
        public bool IsEnabled { get; set; } = true;

        // Conditions (When this rule applies)
        public string ConditionType { get; set; } = "always"; // "always", "time_based", "usage_based", "cost_based", "custom"

        // Time-based conditions
        public string? TimeWindowStart { get; set; } // "09:00"
        public string? TimeWindowEnd { get; set; } // "17:00"
        public string[]? DaysOfWeek { get; set; } // ["Monday", "Tuesday", ...]
        public DateTime? EffectiveFrom { get; set; }
        public DateTime? EffectiveUntil { get; set; }

        // Usage-based conditions
        public decimal? MinUsagePercentage { get; set; } // Minimum budget usage percentage
        public decimal? MaxUsagePercentage { get; set; } // Maximum budget usage percentage
        public int? MinRequestCount { get; set; }
        public int? MaxRequestCount { get; set; }

        // Task/Content-based conditions
        public string? TaskType { get; set; } // "reasoning", "coding", "creative", "analysis"
        public string? ContentType { get; set; } // "text", "code", "json", "markdown"
        public string[]? RequiredKeywords { get; set; } // JSON array
        public string[]? ExcludedKeywords { get; set; } // JSON array

        // Model Selection Criteria (Targets)
        public decimal? MaxCostPerRequest { get; set; }
        public decimal? MaxCostPerThousandTokens { get; set; }
        public int? MinIntelligenceScore { get; set; }
        public decimal? MaxLatencySeconds { get; set; }
        public int? MinContextLength { get; set; }
        public int? MaxOutputTokens { get; set; }

        // Provider & Model Restrictions
        public string[] AllowedProviders { get; set; } = Array.Empty<string>();
        public string[] BlockedProviders { get; set; } = Array.Empty<string>();
        public string[] AllowedModels { get; set; } = Array.Empty<string>();
        public string[] BlockedModels { get; set; } = Array.Empty<string>();

        // Capability Requirements
        public Dictionary<string, decimal> RequiredCapabilities { get; set; } = new();
        public string[] RequiredFeatures { get; set; } = Array.Empty<string>(); // "streaming", "function_calling", "vision"

        // Compliance Requirements
        public string[] RequiredRegions { get; set; } = Array.Empty<string>();
        public bool RequireDataResidency { get; set; }
        public bool RequireEncryptionAtRest { get; set; }
        public bool RequireAuditTrail { get; set; }
        public string[] ComplianceStandards { get; set; } = Array.Empty<string>(); // "GDPR", "HIPAA", "SOC2"

        // Optimization Strategy
        public string OptimizationStrategy { get; set; } = "balanced"; // "cost", "performance", "accuracy", "balanced"
        public Dictionary<string, decimal> StrategyWeights { get; set; } = new()
        {
            ["cost"] = 0.3m,
            ["performance"] = 0.3m,
            ["accuracy"] = 0.3m,
            ["compliance"] = 0.1m
        };

        // Fallback Strategy
        public string FallbackStrategy { get; set; } = "next_best"; // "next_best", "cheapest", "fastest", "same_provider", "disabled"
        public int MaxFallbackAttempts { get; set; } = 3;
        public bool AllowProviderFallback { get; set; } = true;

        // Actions (What to do when rule matches)
        public string ActionType { get; set; } = "select_model"; // "select_model", "adjust_priority", "apply_discount", "notify"
        public string? SelectedModelId { get; set; }
        public string? SelectedProviderId { get; set; }
        public decimal? PriorityAdjustment { get; set; }
        public decimal? CostMultiplier { get; set; } = 1.0m;

        // Notifications & Alerts
        public bool SendNotification { get; set; }
        public string[] NotificationChannels { get; set; } = Array.Empty<string>(); // "email", "slack", "webhook"
        public string? WebhookUrl { get; set; }

        // Advanced Configuration
        public string? CustomLogic { get; set; } // JavaScript/Expression for custom logic
        public string? ExternalServiceId { get; set; } // For integration with external rule engines

        // Performance Tracking
        public int MatchCount { get; set; }
        public int SuccessCount { get; set; }
        public decimal AverageExecutionTime { get; set; }
        public DateTime LastMatchedAt { get; set; }

        // Metadata
        public string[] Tags { get; set; } = Array.Empty<string>();
        public string Version { get; set; } = "1.0";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual Project? Project { get; set; }

        // Helper Methods
        public bool IsConditionMet(ArbitrationContext context)
        {
            // Check time-based conditions
            if (!IsWithinTimeWindow()) return false;

            // Check usage-based conditions
            if (!IsWithinUsageLimits(context)) return false;

            // Check task/content conditions
            if (!MatchesTaskType(context)) return false;

            // Check custom conditions
            if (!EvaluateCustomLogic(context)) return false;

            return true;
        }

        public decimal CalculateModelScore(AIModel model, ArbitrationContext context)
        {
            var scores = new Dictionary<string, double>();

            // Cost score (lower cost = higher score)
            if (model.CostPerMillionInputTokens > 0 && model.CostPerMillionOutputTokens > 0)
            {
                var estimatedCost = CalculateEstimatedCost(model, context);
                var maxAllowedCost = MaxCostPerRequest ?? context.MaxCost ?? 10.0m; // Default $10
                var costScore = (double)Math.Max(0, 100 - (estimatedCost / maxAllowedCost * 100));
                scores["cost"] = costScore;
            }

            // Performance score (faster = higher score)
            if (model.Latency > 0)
            {
                var latencyScore = Math.Max(0, 100 - (model.Latency * 10)); // Scale factor
                scores["performance"] = latencyScore;
            }

            // Intelligence score
            scores["accuracy"] = (double)model.IntelligenceScore;

            // Capability score
            var capabilityScore = CalculateCapabilityScore(model);
            scores["capability"] = (double)capabilityScore;

            // Compliance score
            var complianceScore = CalculateComplianceScore(model, context);
            scores["compliance"] = (double)complianceScore;

            // Apply strategy weights
            var totalScore = 0m;
            foreach (var (key, weight) in StrategyWeights)
            {
                if (scores.TryGetValue(key, out var score))
                {
                    totalScore += (decimal)score * weight;
                }
            }

            return totalScore;
        }

        private bool IsWithinTimeWindow()
        {
            if (TimeWindowStart == null || TimeWindowEnd == null)
                return true;

            var now = DateTime.UtcNow;
            var currentTime = now.TimeOfDay;
            var startTime = TimeSpan.Parse(TimeWindowStart);
            var endTime = TimeSpan.Parse(TimeWindowEnd);

            if (startTime <= endTime)
                return currentTime >= startTime && currentTime <= endTime;
            else // Overnight window
                return currentTime >= startTime || currentTime <= endTime;
        }

        private bool IsWithinUsageLimits(ArbitrationContext context)
        {
            // Implement usage-based condition checking
            return true; // Simplified for now
        }

        private bool MatchesTaskType(ArbitrationContext context)
        {
            if (string.IsNullOrEmpty(TaskType))
                return true;

            return context.TaskType?.Equals(TaskType, StringComparison.OrdinalIgnoreCase) == true;
        }

        private bool EvaluateCustomLogic(ArbitrationContext context)
        {
            if (string.IsNullOrEmpty(CustomLogic))
                return true;

            // In production, use a secure script engine like ClearScript or Jint
            // For now, return true as placeholder
            return true;
        }

        private decimal CalculateEstimatedCost(AIModel model, ArbitrationContext context)
        {
            var avgTokens = GetAverageTokenUsage(context.TaskType);
            var inputCost = (avgTokens.Input / 1_000_000m) * model.CostPerMillionInputTokens;
            var outputCost = (avgTokens.Output / 1_000_000m) * model.CostPerMillionOutputTokens;
            return (inputCost + outputCost) * (CostMultiplier ?? 1.0m);
        }

        private (int Input, int Output) GetAverageTokenUsage(string taskType) => taskType?.ToLower() switch
        {
            "reasoning" => (2000, 1500),
            "coding" => (1000, 800),
            "creative" => (500, 1000),
            "analysis" => (1500, 800),
            _ => (1000, 500)
        };

        private decimal CalculateCapabilityScore(AIModel model)
        {
            if (!RequiredCapabilities.Any()) return 100m;

            var totalScore = 0m;
            var capabilityCount = 0;

            foreach (var (capability, minScore) in RequiredCapabilities)
            {
                var modelScore = model.Capabilities
                    .FirstOrDefault(c => c.CapabilityType.ToString() == capability)?.Score ?? 0;

                if (modelScore >= minScore)
                    totalScore += 100;
                else
                    totalScore += (modelScore / minScore) * 100;

                capabilityCount++;
            }

            return capabilityCount > 0 ? totalScore / capabilityCount : 100m;
        }

        private decimal CalculateComplianceScore(AIModel model, ArbitrationContext context)
        {
            var score = 100m;

            // Check region compliance
            if (RequiredRegions.Any())
            {
                // This would require model region data
                score -= 20;
            }

            // Check data residency
            if (RequireDataResidency && !model.SupportsDataResidency)
                score -= 30;

            // Check encryption
            if (RequireEncryptionAtRest && !model.SupportsEncryptionAtRest)
                score -= 30;

            return Math.Max(0, score);
        }
    }
}
