using AIArbitration.Core.Entities.Enums;

namespace AIArbitration.Core.Entities
{
    public class CostRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Basic Information
        public string RecordType { get; set; } = "model_usage"; // "model_usage", "api_call", "storage", "data_transfer", "custom"
        public string Description { get; set; } = string.Empty;

        // Billing Information
        public string BillingPeriod { get; set; } = string.Empty; // "2024-01", "2024-Q1", "2024"
        public string InvoiceId { get; set; } = string.Empty;
        public string InvoiceItemId { get; set; } = string.Empty;
        public bool IsInvoiced { get; set; }
        public DateTime? InvoicedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public string PaymentStatus { get; set; } = "pending"; // "pending", "paid", "failed", "refunded"
        public string PaymentMethod { get; set; } = string.Empty;
        public string PaymentTransactionId { get; set; } = string.Empty;

        // Cost Details
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public decimal TaxAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal TotalAmount => Amount + TaxAmount - DiscountAmount;

        // Rate Information
        public decimal Rate { get; set; } // Cost per unit
        public string RateUnit { get; set; } = "tokens"; // "tokens", "requests", "gb", "hours"
        public decimal Quantity { get; set; } // Number of units

        // Usage Details (for model usage)
        public string? ModelId { get; set; }
        public string? ModelName { get; set; }
        public string? ProviderId { get; set; }
        public string? ProviderName { get; set; }
        public int? InputTokens { get; set; }
        public int? OutputTokens { get; set; }
        public int? TotalTokens => (InputTokens ?? 0) + (OutputTokens ?? 0);
        public ModelTier ModelTier { get; set; }

        // Resource Information
        public string? ResourceType { get; set; } // "model", "api", "storage", "compute"
        public string? ResourceId { get; set; }
        public string? ResourceName { get; set; }
        public string? ServiceName { get; set; } = "ai_arbitration";

        // User/Tenant Context
        public string TenantId { get; set; } = string.Empty;
        public string? ProjectId { get; set; }
        public string? UserId { get; set; }
        public string? ApiKeyId { get; set; }
        public string? SessionId { get; set; }

        // Request Context
        public string? RequestId { get; set; }
        public string? CorrelationId { get; set; }
        public string? Endpoint { get; set; }
        public string? Operation { get; set; } // "chat_completion", "embeddings", "fine_tuning"

        // Performance Metrics
        public TimeSpan? Duration { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }

        // Geographic Information
        public string? Region { get; set; }
        public string? DataCenter { get; set; }

        // Metadata
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
        public string[] Tags { get; set; } = Array.Empty<string>();

        // Cost Allocation
        public string? CostCenter { get; set; }
        public string? Department { get; set; }
        public string? Team { get; set; }
        public string? ProjectCode { get; set; }

        // Audit & Compliance
        public bool IsEstimated { get; set; }
        public bool IsAdjusted { get; set; }
        public string? AdjustmentReason { get; set; }
        public string? AdjustedBy { get; set; }
        public DateTime? AdjustedAt { get; set; }

        // Retention & Archiving
        public int RetentionDays { get; set; } = 1825; // 5 years for financial records
        public DateTime? ArchivedAt { get; set; }
        public string? ArchiveLocation { get; set; }

        // Timestamps
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // Navigation Properties
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual Project? Project { get; set; }
        public virtual ApplicationUser? User { get; set; }
        public virtual ApiKey? ApiKey { get; set; }
        public virtual UserSession? Session { get; set; }

        // Helper Methods
        public decimal CalculateCostPerToken()
        {
            if (TotalTokens == 0 || TotalTokens == null) return 0;
            return Amount / (TotalTokens ?? 1);
        }

        public decimal CalculateCostPerRequest()
        {
            return Amount / (Quantity > 0 ? Quantity : 1);
        }

        public bool IsWithinBudget(decimal budgetAmount)
        {
            return TotalAmount <= budgetAmount;
        }

        public decimal GetCostPercentageOfBudget(decimal budgetAmount)
        {
            if (budgetAmount <= 0) return 0;
            return (TotalAmount / budgetAmount) * 100;
        }
    }
}