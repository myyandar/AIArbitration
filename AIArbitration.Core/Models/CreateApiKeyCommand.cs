using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public class CreateApiKeyCommand
    {
        public string UserId { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
