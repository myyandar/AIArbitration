using System.Text.Json;

namespace AIArbitration.Core.Entities
{
    public class TenantSetting
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // Tenant reference
        public string TenantId { get; set; } = string.Empty;

        // Setting details
        public string SettingKey { get; set; } = string.Empty;
        public string SettingValue { get; set; } = string.Empty;
        public string DataType { get; set; } = "string"; // "string", "int", "bool", "decimal", "json"
        public string Category { get; set; } = "general"; // "general", "billing", "security", "compliance", "notifications"
        public string? Description { get; set; }
        public bool IsEncrypted { get; set; }
        public bool IsRequired { get; set; }
        public bool IsReadOnly { get; set; }

        // Validation
        public string? ValidationRules { get; set; } // JSON validation rules
        public string? AllowedValues { get; set; } // JSON array for enum-like settings

        // Versioning
        public string Version { get; set; } = "1.0";
        public string? DefaultValue { get; set; }

        // Tracking
        public string? LastModifiedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation
        public virtual Tenant Tenant { get; set; } = null!;

        // Helper methods
        public T GetValue<T>()
        {
            if (string.IsNullOrEmpty(SettingValue))
                return default(T);

            if (DataType == "json")
                return JsonSerializer.Deserialize<T>(SettingValue);

            return (T)Convert.ChangeType(SettingValue, typeof(T));
        }

        public void SetValue<T>(T value)
        {
            if (DataType == "json")
                SettingValue = JsonSerializer.Serialize(value);
            else
                SettingValue = value?.ToString() ?? string.Empty;
        }
    }

    // Common tenant setting keys
    public static class TenantSettingKeys
    {
        // General
        public const string DefaultRegion = "general.default_region";
        public const string TimeZone = "general.timezone";
        public const string DateFormat = "general.date_format";
        public const string Language = "general.language";

        // Billing
        public const string BillingCurrency = "billing.currency";
        public const string InvoicePrefix = "billing.invoice_prefix";
        public const string TaxRate = "billing.tax_rate";
        public const string InvoiceTemplate = "billing.invoice_template";

        // Security
        public const string SessionTimeout = "security.session_timeout";
        public const string RequireMFA = "security.require_mfa";
        public const string PasswordPolicy = "security.password_policy";
        public const string IpWhitelist = "security.ip_whitelist";
        public const string FailedLoginAttempts = "security.failed_login_attempts";

        // Compliance
        public const string DataRetentionDays = "compliance.data_retention_days";
        public const string AuditLogRetention = "compliance.audit_log_retention";
        public const string RequireConsent = "compliance.require_consent";
        public const string DefaultComplianceRegion = "compliance.default_region";

        // Notifications
        public const string AdminEmail = "notifications.admin_email";
        public const string BillingEmail = "notifications.billing_email";
        public const string SendBudgetAlerts = "notifications.send_budget_alerts";
        public const string BudgetAlertThreshold = "notifications.budget_alert_threshold";

        // AI/Model Settings
        public const string DefaultModel = "ai.default_model";
        public const string CostOptimizationStrategy = "ai.cost_optimization_strategy";
        public const string MaxCostPerRequest = "ai.max_cost_per_request";
        public const string AllowedProviders = "ai.allowed_providers";
    }
}