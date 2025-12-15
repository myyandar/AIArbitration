using System;
using AIArbitration.Core.Models;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class OpenAiToolCall
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public OpenAiFunctionCall Function { get; set; } = new();
    }
}
