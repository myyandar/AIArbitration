using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Core.Services;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IComplianceService
    {
        // Compliance checking
        Task<ComplianceCheckResult> CheckModelComplianceAsync(AIModel model, ArbitrationContext context);
        Task<ComplianceCheckResult> CheckRequestComplianceAsync(ChatRequest request, ArbitrationContext context);
        Task<ComplianceCheckResult> CheckResponseComplianceAsync(ModelResponse response, ArbitrationContext context);

        // Batch compliance checks
        Task<List<ComplianceCheckResult>> CheckModelsComplianceAsync(List<AIModel> models, ArbitrationContext context);
        Task<List<ComplianceCheckResult>> CheckRequestsComplianceAsync(List<ChatRequest> requests, ArbitrationContext context);

        // Rule management
        Task<List<ComplianceRule>> GetComplianceRulesAsync(string tenantId);
        Task<ComplianceRule> GetComplianceRuleAsync(string ruleId);
        Task<ComplianceRule> CreateComplianceRuleAsync(ComplianceRule rule);
        Task<ComplianceRule> UpdateComplianceRuleAsync(ComplianceRule rule);
        Task DeleteComplianceRuleAsync(string ruleId);

        // Compliance validation
        Task<ComplianceValidationResult> ValidateTenantComplianceAsync(string tenantId);
        Task<ComplianceValidationResult> ValidateProjectComplianceAsync(string projectId);
        Task<ComplianceValidationResult> ValidateUserComplianceAsync(string userId);

        // Logging and auditing
        Task LogComplianceCheckAsync(ComplianceLog log);
        Task<List<ComplianceLog>> GetComplianceLogsAsync(string tenantId, DateTime start, DateTime end);

        // Data handling
        Task<DataHandlingResult> HandleDataRequestAsync(DataRequest request);
        Task<DataDeletionResult> DeleteUserDataAsync(string userId);
        Task<DataExportResult> ExportUserDataAsync(string userId);

        // Compliance reporting
        Task<ComplianceReport> GenerateComplianceReportAsync(string tenantId, DateTime start, DateTime end);
        Task<List<ComplianceViolation>> GetComplianceViolationsAsync(string tenantId, DateTime start, DateTime end);

        // Configuration
        Task<ComplianceConfiguration> GetComplianceConfigurationAsync(string tenantId);
        Task UpdateComplianceConfigurationAsync(string tenantId, ComplianceConfiguration configuration);
    }
}
