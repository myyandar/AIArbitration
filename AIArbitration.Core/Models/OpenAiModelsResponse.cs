using AIArbitration.Core.Entities;

namespace AIArbitration.Core.Models
{
    public class OpenAiModelsResponse
    {
        public string Object { get; set; } = string.Empty;
        public List<OpenAiModel> Data { get; set; } = new();
    }
}
