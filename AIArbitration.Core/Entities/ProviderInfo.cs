using AIArbitration.Core.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class ProviderInfo
    {
        public HealthStatus healthStatus;
        public List<string> supportedModels;

        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string HealthStatus { get; set; } = string.Empty;
        public List<string> SupportedModels { get; set; } = new();
    }
}
