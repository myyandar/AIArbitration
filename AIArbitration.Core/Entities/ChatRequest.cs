using AIArbitration.Core.Models;

namespace AIArbitration.Core.Entities
{
    public class ChatRequest
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public required string ModelId { get; set; } // The model to use (or "arbitrated" for engine to decide)
        public required List<ChatMessage> Messages { get; set; }
        public decimal? Temperature { get; set; } = 0.7m;
        public decimal? TopP { get; set; } = 1.0m;
        public int? MaxTokens { get; set; }
        public decimal? FrequencyPenalty { get; set; } = 0.0m;
        public decimal? PresencePenalty { get; set; } = 0.0m;
        public bool Stream { get; set; } = false;
        public int? Seed { get; set; }
        public string? Stop { get; set; }
        public List<string>? StopSequences { get; set; }

        // Model-specific parameters
        public Dictionary<string, object>? Parameters { get; set; }

        // Request context
        public string? RequestId { get; set; }
        public string? SessionId { get; set; }
        public string? UserId { get; set; }
        public string? TenantId { get; set; }
        public string? ProjectId { get; set; }

        // Function calling
        public string? FunctionCall { get; set; } // "auto", "none", or function name
        public IEnumerable<ToolCall> Tools { get; set; } = Enumerable.Empty<ToolCall>();

        // Tool calling
        public string? ToolChoice { get; set; } // "auto", "none", or specific tool

        // Compliance
        public bool? StoreContent { get; set; } = true; // Whether to store in audit logs
        public string[]? ContentCategories { get; set; } // For compliance classification
        public string? ComplianceRegion { get; set; }

        // Cost constraints
        public decimal? MaxCost { get; set; }
        public int? MaxRetries { get; set; } = 3;

        // Custom metadata
        public Dictionary<string, string>? Metadata { get; set; }
        public string? SystemPrompt { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

        // Compliance-related properties
        public string? Purpose { get; set; }
        public string? Region { get; set; }
        public bool RequiresEncryption { get; set; }
        public Dictionary<string, bool> Consents { get; set; } = new();
        public List<string> SensitiveDataTypes { get; set; } = new();

        public virtual Tenant Tenant { get; set; } = null!;
        public virtual AIModel Model { get; set; } = null!;
    }
}
