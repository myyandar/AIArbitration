namespace AIArbitration.Core.Models
{
    public class ToolDefinition
    {
        public required string Type { get; set; } = "function";
        public required FunctionDefinition Function { get; set; }
    }
}
