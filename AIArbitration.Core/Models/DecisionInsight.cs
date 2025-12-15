using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class DecisionInsight
    {
        public string TaskType { get; set; } = string.Empty;
        public List<ModelRecommendation> RecommendedModels { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }
}
