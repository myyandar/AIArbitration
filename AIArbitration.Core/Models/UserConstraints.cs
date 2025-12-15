using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class UserConstraints
    {
        public List<string> AllowedModels { get; set; } = new();
        public List<string> BlockedModels { get; set; } = new();
        public List<string> AllowedProviders { get; set; } = new();
        public List<string> BlockedProviders { get; set; } = new();
        public decimal? MaxCostPerRequest { get; set; }
        public decimal? DailyCostLimit { get; set; }
        public decimal? MonthlyCostLimit { get; set; }
        public int? MaxRequestsPerMinute { get; set; }
        public int? MaxRequestsPerHour { get; set; }
        public int? MaxRequestsPerDay { get; set; }
        public int? MaxTokensPerRequest { get; set; }
        public int? MaxTotalTokensPerDay { get; set; }
        public string? DefaultModel { get; set; }
        public string UserId { get; set; }
        public string TenantId { get; set; }
        public List<string> PreferredModels { get; set; } = new();
        public Dictionary<CapabilityType, decimal> RequiredCapabilities { get; set; } = new();

        // Time restrictions
        public TimeSpan? AllowedStartTime { get; set; }
        public TimeSpan? AllowedEndTime { get; set; }
        public List<DayOfWeek> AllowedDays { get; set; } = new();

        // Geographic restrictions
        public List<string> AllowedCountries { get; set; } = new();
        public List<string> BlockedCountries { get; set; } = new();

        // Content restrictions
        public List<string> BlockedKeywords { get; set; } = new();
        public List<string> AllowedContentTypes { get; set; } = new();

        // Compliance
        public bool RequireDataResidency { get; set; }
        public string? RequiredDataRegion { get; set; }

        public bool IsWithinTimeRestrictions()
        {
            var now = DateTime.UtcNow;

            // Check time window
            if (AllowedStartTime.HasValue && AllowedEndTime.HasValue)
            {
                var currentTime = now.TimeOfDay;
                if (currentTime < AllowedStartTime.Value || currentTime > AllowedEndTime.Value)
                    return false;
            }

            // Check days of week
            if (AllowedDays.Any() && !AllowedDays.Contains(now.DayOfWeek))
                return false;

            return true;
        }

        public bool IsCountryAllowed(string countryCode)
        {
            if (string.IsNullOrEmpty(countryCode)) return true;

            if (BlockedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
                return false;

            if (AllowedCountries.Any() && !AllowedCountries.Contains(countryCode, StringComparer.OrdinalIgnoreCase))
                return false;

            return true;
        }
    }
}
