using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class ArbitrationConfiguration
    {
        public List<ArbitrationRule> Rules { get; set; } = new();
        public List<ModelProvider> AvailableProviders { get; set; } = new();
        public Dictionary<string, object> Settings { get; set; } = new();
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = "1.0";
        public IEnumerable<AIModel> AvailableModels { get; set; }
    }
}
