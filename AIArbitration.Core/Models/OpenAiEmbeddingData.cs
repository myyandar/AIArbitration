namespace AIArbitration.Core.Models
{
    public class OpenAiEmbeddingData
    {
        public int Index { get; set; }
        public List<float> Embedding { get; set; } = new();
        public string Object { get; set; } = string.Empty;
    }
}
