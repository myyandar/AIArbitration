using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Entities.Enums
{
    public static class AuditCategory
    {
        public const string Authentication = "authentication";
        public const string Authorization = "authorization";
        public const string UserManagement = "user_management";
        public const string TenantManagement = "tenant_management";
        public const string ProjectManagement = "project_management";
        public const string ModelManagement = "model_management";
        public const string Arbitration = "arbitration";
        public const string BudgetManagement = "budget_management";
        public const string CostTracking = "cost_tracking";
        public const string Security = "security";
        public const string Compliance = "compliance";
        public const string System = "system";
        public const string ProviderManagement = "provider_management";
        public const string AuditTrail = "audit_trail";
        public const string DataProcessing = "data_processing";
        public const string Network = "network";
        public const string Performance = "performance";
    }
}
