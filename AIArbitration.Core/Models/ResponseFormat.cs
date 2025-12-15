namespace AIArbitration.Core.Models
{
    public class ResponseFormat
    {
        public string Type { get; set; } = "text"; // "text" or "json_object"
        public string? Schema { get; set; } // JSON Schema for json_object type
    }

}
