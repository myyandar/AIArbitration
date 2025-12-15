using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    // Arbitration.Core/Models/BudgetStatus.cs
    public class BudgetStatus
    {
        // Basic Information
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string BudgetId { get; set; } = string.Empty;

        // Budget Configuration
        public decimal BudgetAmount { get; set; }
        public string Currency { get; set; } = "USD";
        public BudgetPeriod Period { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }

        // Usage Tracking
        public decimal UsedAmount { get; set; }
        public decimal RemainingAmount => BudgetAmount - UsedAmount;
        public decimal UsagePercentage => BudgetAmount > 0 ? (UsedAmount / BudgetAmount) * 100 : 0;

        // Thresholds & Alerts
        public decimal WarningThreshold { get; set; } = 80; // 80%
        public decimal CriticalThreshold { get; set; } = 95; // 95%

        public bool IsWarningThresholdReached => UsagePercentage >= WarningThreshold;
        public bool IsCriticalThresholdReached => UsagePercentage >= CriticalThreshold;
        public bool IsOverBudget => UsedAmount > BudgetAmount;

        // Status Flags
        public BudgetHealthStatus BudgetHealthStatus 
        {
            get
            {
                if (IsOverBudget) return BudgetHealthStatus.OverBudget;
                if (IsCriticalThresholdReached) return BudgetHealthStatus.Critical;
                if (IsWarningThresholdReached) return BudgetHealthStatus.Warning;
                return BudgetHealthStatus.Healthy;
            }
        }

        public bool CanMakeRequest(decimal estimatedCost)
        {
            return !IsOverBudget && (RemainingAmount >= estimatedCost);
        }

        // Rate Limiting
        public int RequestCount { get; set; }
        public int MaxRequestsPerPeriod { get; set; }
        public decimal RequestCountPercentage => MaxRequestsPerPeriod > 0 ?
            ((decimal)RequestCount / MaxRequestsPerPeriod) * 100 : 0;

        public bool IsRequestLimitReached => MaxRequestsPerPeriod > 0 &&
            RequestCount >= MaxRequestsPerPeriod;

        // Time-based Status
        public TimeSpan TimeRemaining => PeriodEnd - DateTime.UtcNow;
        public decimal TimeElapsedPercentage
        {
            get
            {
                var totalPeriod = PeriodEnd - PeriodStart;
                if (totalPeriod.TotalSeconds <= 0) return 0;

                var elapsed = DateTime.UtcNow - PeriodStart;
                return (decimal)(elapsed.TotalSeconds / totalPeriod.TotalSeconds) * 100;
            }
        }

        // Cost Efficiency Metrics
        public decimal CostPerRequest => RequestCount > 0 ? UsedAmount / RequestCount : 0;
        public decimal CostPerToken { get; set; }
        public decimal AverageRequestCost { get; set; }

        // Model Usage Breakdown
        public Dictionary<string, decimal> CostByModel { get; set; } = new();
        public Dictionary<string, decimal> CostByProvider { get; set; } = new();

        // Notifications
        public bool WarningNotificationSent { get; set; }
        public bool CriticalNotificationSent { get; set; }
        public bool OverBudgetNotificationSent { get; set; }

        // Last Updates
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public DateTime? LastWarningNotificationAt { get; set; }
        public DateTime? LastCriticalNotificationAt { get; set; }
        public DateTime? LastOverBudgetNotificationAt { get; set; }

        // Forecast
        public decimal ForecastedUsage
        {
            get
            {
                if (TimeElapsedPercentage <= 0) return 0;

                // Simple linear forecast
                var projectedUsage = (UsagePercentage / TimeElapsedPercentage) * 100;
                return (projectedUsage / 100) * BudgetAmount;
            }
        }

        public bool IsForecastedToExceed => ForecastedUsage > BudgetAmount;

        // Historical Comparison
        public decimal? PreviousPeriodUsage { get; set; }
        public decimal? UsageChangePercentage => PreviousPeriodUsage.HasValue && PreviousPeriodUsage.Value > 0 ?
            ((UsedAmount - PreviousPeriodUsage.Value) / PreviousPeriodUsage.Value) * 100 : null;

        // Helper Methods
        public decimal GetAvailableForRequest(decimal estimatedCost)
        {
            return Math.Max(0, RemainingAmount - estimatedCost);
        }

        public decimal GetSafeRequestLimit(decimal averageRequestCost)
        {
            if (averageRequestCost <= 0) return RemainingAmount;
            return RemainingAmount / averageRequestCost;
        }

        public TimeSpan GetEstimatedTimeUntilExhaustion(decimal averageHourlyCost)
        {
            if (averageHourlyCost <= 0) return TimeSpan.MaxValue;

            var hoursRemaining = (double)(RemainingAmount / averageHourlyCost);
            return TimeSpan.FromHours(hoursRemaining);
        }
    }
}
