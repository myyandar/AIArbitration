using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    public class StreamingChunkData
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<StreamingChoice> Choices { get; set; } = new();
        public DateTime Created { get; set; }
    }
}
