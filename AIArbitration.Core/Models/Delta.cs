namespace AIArbitration.Core.Models
{
    // Supporting class for streaming responses
    public class Delta
    {
        public string? Role { get; set; }
        public string? Content { get; set; }
        public string? StopReason { get; set; }
    }
}
