namespace AIArbitration.Core.Entities
{
    public class EmbeddingData
    {
        public int Index { get; set; }
        public List<float> Embedding { get; set; } = new List<float>();

        // Optional properties that might be useful
        public string? Object { get; set; }
    }
}
