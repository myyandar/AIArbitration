using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface ITenantService
    {
        // Tenant management
        Task<Tenant?> GetTenantByIdAsync(string tenantId);
        Task<Tenant?> GetTenantByDomainAsync(string domain);
        Task<List<Tenant>> GetAllTenantsAsync();
        Task<Tenant> CreateTenantAsync(CreateTenantCommand command);
        Task<Tenant> UpdateTenantAsync(UpdateTenantCommand command);
        Task DeleteTenantAsync(string tenantId);

        // Tenant constraints and settings
        Task<TenantConstraints> GetTenantConstraintsAsync(string tenantId);
        Task UpdateTenantConstraintsAsync(string tenantId, TenantConstraints constraints);
        Task<TenantSettings> GetTenantSettingsAsync(string tenantId);
        Task UpdateTenantSettingsAsync(string tenantId, TenantSettings settings);

        // User management
        Task<List<ApplicationUser>> GetTenantUsersAsync(string tenantId);
        Task<ApplicationUser> AddUserToTenantAsync(string tenantId, AddUserCommand command);
        Task RemoveUserFromTenantAsync(string tenantId, string userId);

        // Project management
        Task<List<Project>> GetTenantProjectsAsync(string tenantId);
        Task<Project> CreateProjectAsync(CreateProjectCommand command);
        Task<Project> UpdateProjectAsync(UpdateProjectCommand command);
        Task DeleteProjectAsync(string projectId);

        // Budget management
        Task<List<BudgetAllocation>> GetTenantBudgetsAsync(string tenantId);

        // Task<BudgetAllocation> CreateBudgetAsync(CreateBudgetCommand command);
        Task<BudgetAllocation> UpdateBudgetAsync(UpdateBudgetCommand command);
        // Duplicate
        //Task DeleteBudgetAsync(string budgetId);

        // Compliance
        Task<List<ComplianceRule>> GetTenantComplianceRulesAsync(string tenantId);
        Task<ComplianceRule> CreateComplianceRuleAsync(CreateComplianceRuleCommand command);
        Task<ComplianceRule> UpdateComplianceRuleAsync(UpdateComplianceRuleCommand command);

        // Analytics
        Task<TenantAnalytics> GetTenantAnalyticsAsync(string tenantId, DateTime start, DateTime end);
        Task<TenantStatistics> GetTenantStatisticsAsync(string tenantId);

        // Billing and subscriptions
        Task<Subscription> GetTenantSubscriptionAsync(string tenantId, bool isActive, TenantPlan plan);
        Task<Subscription> UpdateSubscriptionAsync(string tenantId, UpdateSubscriptionCommand command);
        Task<List<Invoice>> GetTenantInvoicesAsync(string tenantId);
        Task<Invoice> GenerateInvoiceAsync(decimal tax, decimal discount, decimal serviceFee, string tenantId, DateTime periodStart, DateTime periodEnd);
    }
}
