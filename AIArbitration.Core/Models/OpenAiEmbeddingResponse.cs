namespace AIArbitration.Core.Models
{
    public class OpenAiEmbeddingResponse
    {
        public string Object { get; set; } = string.Empty;
        public List<OpenAiEmbeddingData> Data { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public OpenAiUsage Usage { get; set; } = new();
    }
}
