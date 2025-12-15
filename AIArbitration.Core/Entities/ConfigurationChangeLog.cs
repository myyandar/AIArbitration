using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    /// <summary>
    /// Configuration change log
    /// </summary>
    public class ConfigurationChangeLog
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string ProviderId { get; set; } = string.Empty;
        public string ChangedBy { get; set; } = string.Empty;
        public string ChangeType { get; set; } = string.Empty; // Create, Update, Delete
        public string OldConfiguration { get; set; } = string.Empty;
        public string NewConfiguration { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
