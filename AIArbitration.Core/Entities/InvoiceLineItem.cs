using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities
{
    /// <summary>
    /// Invoice line item
    /// </summary>
    public class InvoiceLineItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string InvoiceId { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? ModelId { get; set; }
        public string? Provider { get; set; }
        public int Quantity { get; set; } // Could be requests or tokens
        public string Unit { get; set; } = "request"; // "request", "token", "hour", etc.
        public decimal UnitPrice { get; set; }
        public decimal Amount => Quantity * UnitPrice;
        public decimal TaxRate { get; set; }
        public decimal TaxAmount => Amount * TaxRate;
        public string? ProjectId { get; set; }
        public DateTime? UsagePeriodStart { get; set; }
        public DateTime? UsagePeriodEnd { get; set; }
    }
}
