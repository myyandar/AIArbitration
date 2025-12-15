using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    // Supporting class for chat message content parts
    public class ContentPart
    {
        public string Type { get; set; } = string.Empty; // "text", "image", etc.
        public string? Text { get; set; }
        public string? ImageUrl { get; set; }
        public Dictionary<string, object>? Details { get; set; }
    }
}
