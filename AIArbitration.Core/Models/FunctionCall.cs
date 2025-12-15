namespace AIArbitration.Core.Models
{
    public class FunctionCall
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty; // JSON string
    }
}
