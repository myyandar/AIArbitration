using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class OpenAiModerationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<OpenAiModerationResult> Results { get; set; } = new();
        public Dictionary<string, ModerationCategory> Categories { get; set; } = new();
    }
}
