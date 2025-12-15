using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class ToolCall
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Type { get; set; } = "function";
        public FunctionDefinition Function { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] Parameters { get; set; } = Array.Empty<string>();
    }
}
