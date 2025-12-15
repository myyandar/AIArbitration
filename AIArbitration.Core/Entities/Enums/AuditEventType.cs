using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public static class AuditEventType
    {
        // Authentication Events
        public const string UserLogin = "user_login";
        public const string UserLogout = "user_logout";
        public const string UserRegister = "user_register";
        public const string PasswordChange = "password_change";
        public const string PasswordReset = "password_reset";
        public const string MfaEnabled = "mfa_enabled";
        public const string MfaDisabled = "mfa_disabled";
        public const string MfaUsed = "mfa_used";
        public const string LoginFailed = "login_failed";
        public const string AccountLocked = "account_locked";
        public const string AccountUnlocked = "account_unlocked";

        // API Key Events
        public const string ApiKeyCreated = "api_key_created";
        public const string ApiKeyRevoked = "api_key_revoked";
        public const string ApiKeyRotated = "api_key_rotated";
        public const string ApiKeyUsed = "api_key_used";
        public const string ApiKeyExpired = "api_key_expired";

        // User Management Events
        public const string UserCreated = "user_created";
        public const string UserUpdated = "user_updated";
        public const string UserDeleted = "user_deleted";
        public const string UserRoleChanged = "user_role_changed";
        public const string UserPermissionsChanged = "user_permissions_changed";

        // Tenant Management Events
        public const string TenantCreated = "tenant_created";
        public const string TenantUpdated = "tenant_updated";
        public const string TenantDeleted = "tenant_deleted";
        public const string TenantSuspended = "tenant_suspended";
        public const string TenantReactivated = "tenant_reactivated";

        // Project Management Events
        public const string ProjectCreated = "project_created";
        public const string ProjectUpdated = "project_updated";
        public const string ProjectDeleted = "project_deleted";
        public const string ProjectMemberAdded = "project_member_added";
        public const string ProjectMemberRemoved = "project_member_removed";

        // Model Events
        public const string ModelUsed = "model_used";
        public const string ModelSwitched = "model_switched";
        public const string ModelAdded = "model_added";
        public const string ModelUpdated = "model_updated";
        public const string ModelRemoved = "model_removed";
        public const string ModelPerformance = "model_performance";

        // AIArbitration Events
        public const string ArbitrationDecision = "arbitration_decision";
        public const string ModelSelected = "model_selected";
        public const string FallbackUsed = "fallback_used";
        public const string ProviderSwitched = "provider_switched";
        public const string CostOptimized = "cost_optimized";

        // Budget & Cost Events
        public const string BudgetCreated = "budget_created";
        public const string BudgetUpdated = "budget_updated";
        public const string BudgetDeleted = "budget_deleted";
        public const string BudgetExceeded = "budget_exceeded";
        public const string BudgetWarning = "budget_warning";
        public const string CostRecorded = "cost_recorded";
        public const string InvoiceGenerated = "invoice_generated";
        public const string PaymentProcessed = "payment_processed";

        // Security Events
        public const string SecurityViolation = "security_violation";
        public const string SuspiciousActivity = "suspicious_activity";
        public const string BruteForceAttempt = "brute_force_attempt";
        public const string DataExfiltrationAttempt = "data_exfiltration_attempt";
        public const string UnauthorizedAccess = "unauthorized_access";
        public const string PermissionEscalation = "permission_escalation";
        public const string RateLimitExceeded = "rate_limit_exceeded";
        public const string IpBlacklisted = "ip_blacklisted";
        public const string IpWhitelisted = "ip_whitelisted";

        // Compliance Events
        public const string DataAccessRequest = "data_access_request";
        public const string DataDeletionRequest = "data_deletion_request";
        public const string DataRectificationRequest = "data_rectification_request";
        public const string ConsentGiven = "consent_given";
        public const string ConsentRevoked = "consent_revoked";
        public const string PrivacyPolicyAccepted = "privacy_policy_accepted";
        public const string TermsAccepted = "terms_accepted";

        // System Events
        public const string SystemStartup = "system_startup";
        public const string SystemShutdown = "system_shutdown";
        public const string ConfigurationChanged = "configuration_changed";
        public const string BackupCreated = "backup_created";
        public const string RestorePerformed = "restore_performed";
        public const string MaintenanceStarted = "maintenance_started";
        public const string MaintenanceCompleted = "maintenance_completed";

        // Provider Events
        public const string ProviderAdded = "provider_added";
        public const string ProviderUpdated = "provider_updated";
        public const string ProviderRemoved = "provider_removed";
        public const string ProviderHealthCheck = "provider_health_check";
        public const string ProviderIncident = "provider_incident";
        public const string ProviderRecovered = "provider_recovered";

        // Audit Trail Events
        public const string AuditLogExported = "audit_log_exported";
        public const string AuditLogArchived = "audit_log_archived";
        public const string AuditLogPurged = "audit_log_purged";
        public const string BlockchainAuditLogged = "blockchain_audit_logged";
    }
}
