using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Models
{
    public class CapabilityRequirement
    {
        public CapabilityType Type { get; set; }
        public decimal MinScore { get; set; } = 70;
    }
}
