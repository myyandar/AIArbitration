namespace AIArbitration.Core.Models
{
    public class SubscriptionFeature
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int Limit { get; set; }
        public int Used { get; set; }
        public bool IsUnlimited { get; set; }
    }
}
