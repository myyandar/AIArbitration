using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class OpenAiChatCompletionResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string Model { get; set; } = string.Empty;
        public List<OpenAiChoice> Choices { get; set; } = new();
        public OpenAiUsage Usage { get; set; } = new();
        public string? SystemFingerprint { get; set; }
    }
}
