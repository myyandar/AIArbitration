using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class PerformanceAnomaly
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string PerformanceAnalysisId { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Deviation { get; set; }

        public virtual PerformanceAnalysis PerformanceAnalysis { get; set; } = null!;
    }
}
