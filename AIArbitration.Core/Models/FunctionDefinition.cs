namespace AIArbitration.Core.Models
{
    public class FunctionDefinition
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string[] Parameters { get; set; } = Array.Empty<string>();
        public bool? Strict { get; set; }
        public string? Arguments { get; set; }
    }
}
