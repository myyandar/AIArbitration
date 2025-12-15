namespace AIArbitration.Core.Models
{
    public class EmbeddingRequest
    {
        public string ModelId { get; set; } = string.Empty;
        public List<string> Inputs { get; set; } = new();
        public Dictionary<string, object>? Parameters { get; set; }
        public string? UserId { get; set; }
        public string? Id { get; set; }
    }
}
