namespace AIArbitration.Core.Models
{
    public class ModelPreference
    {
        public string ModelId { get; set; } = string.Empty;
        public decimal Weight { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
