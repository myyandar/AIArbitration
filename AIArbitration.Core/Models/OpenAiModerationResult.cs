namespace AIArbitration.Core.Models
{
    public class OpenAiModerationResult
    {
        public bool Flagged { get; set; }
        public Dictionary<string, ModerationCategory> Categories { get; set; } = new();
        public Dictionary<string, decimal> CategoryScores { get; set; } = new();
    }
}
