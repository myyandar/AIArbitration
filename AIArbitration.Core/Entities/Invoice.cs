using AIArbitration.Core.Entities.Enums;
using Microsoft.AspNetCore.Http;
using System.Security.Cryptography.Pkcs;

namespace AIArbitration.Core.Entities
{
    public class Invoice
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string TenantId { get; set; } = string.Empty;
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public DateTime IssueDate { get; set; } = DateTime.UtcNow;
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(30);

        // Financial details
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal Total => Subtotal + Tax;
        public string Currency { get; set; } = "USD";
        public string PaymentStatus { get; set; } = "pending"; // pending, paid, overdue, cancelled
        public DateTime? PaidAt { get; set; }
        public string? PaymentMethod { get; set; }
        public string? PaymentTransactionId { get; set; }

        // Line items
        public List<InvoiceLineItem> LineItems { get; set; } = new();

        // Metadata
        public Dictionary<string, object> Metadata { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Financial details
        public decimal ServiceFee { get; set; }
        public decimal Discount { get; set; }
        public decimal TotalAmount { get; set; }

        // Payment status
        public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
        public DateTime? PaidDate { get; set; }
        public string? TransactionId { get; set; }

        // Usage summary
        public int TotalRequests { get; set; }
        public long TotalTokens { get; set; }
        public Dictionary<string, decimal> CostBreakdown { get; set; } = new();

        // Billing information
        public BillingAddress BillingAddress { get; set; } = new();
        public ContactInfo ContactInfo { get; set; } = new();

        // Metadata
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? Notes { get; set; }

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;

        public decimal GetTotalAmount()
        {         
            return Subtotal + Tax + ServiceFee - Discount;
        }
    }
   /// <summary>
    /// Billing address
    /// </summary>
    public class BillingAddress
    {
        public string CompanyName { get; set; } = string.Empty;
        public string AddressLine1 { get; set; } = string.Empty;
        public string? AddressLine2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string? TaxId { get; set; }
    }

    /// <summary>
    /// Contact information
    /// </summary>
    public class ContactInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
    }
}
