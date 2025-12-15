namespace AIArbitration.Core.Models
{
    public class OpenAiChoice
    {
        public int Index { get; set; }
        public OpenAiMessage Message { get; set; } = new();
        public string? FinishReason { get; set; }
    }
}
