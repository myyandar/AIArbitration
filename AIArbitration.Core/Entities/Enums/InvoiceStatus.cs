using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    /// <summary>
    /// Invoice status
    /// </summary>
    public enum InvoiceStatus
    {
        Draft = 0,
        Pending,
        Processing,
        Paid,
        Overdue,
        Cancelled,
        Refunded
    }
}
