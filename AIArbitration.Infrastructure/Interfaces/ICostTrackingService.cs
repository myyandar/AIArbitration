using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;

namespace AIArbitration.Infrastructure.Interfaces
{
    /// <summary>
    /// Service interface for tracking and managing AI service usage costs
    /// </summary>
    public interface ICostTrackingService
    {
        // Record usage
        // Duplicate
        Task RecordUsageAsync(UsageRecord usage);
        Task RecordBatchUsageAsync(List<UsageRecord> usages);

        // Cost queries
        Task<decimal> GetTotalCostAsync(string tenantId, DateTime start, DateTime end);
        Task<decimal> GetTotalCostAsync(string tenantId, string projectId, DateTime start, DateTime end);
        Task<decimal> GetTotalCostAsync(string tenantId, string userId, string projectId, DateTime start, DateTime end);

        // Cost breakdowns
        Task<Dictionary<string, decimal>> GetCostBreakdownByModelAsync(string tenantId, DateTime start, DateTime end);
        Task<Dictionary<string, decimal>> GetCostBreakdownByProviderAsync(string tenantId, DateTime start, DateTime end);
        Task<Dictionary<string, decimal>> GetCostBreakdownByProjectAsync(string tenantId, DateTime start, DateTime end);

        // Usage statistics
        Task<UsageStatistics> GetUsageStatisticsAsync(string tenantId, DateTime start, DateTime end);
        Task<List<UsageRecord>> GetUsageRecordsAsync(string tenantId, DateTime start, DateTime end, int limit = 100);

        // Cost estimation
        Task<CostEstimation> EstimateCostAsync(string modelId, int inputTokens, int outputTokens);

        // Budget tracking
        // Duplicate
        // Task<BudgetStatus> GetBudgetStatusAsync(string tenantId, string? projectId = null, string? userId = null);
        Task<bool> IsWithinBudgetAsync(string tenantId, decimal estimatedCost, string? projectId = null, string? userId = null);

        // Invoice generation
        Task<Invoice> GenerateInvoiceAsync(string tenantId, DateTime periodStart, DateTime periodEnd);
        Task<List<Invoice>> GetInvoicesAsync(string tenantId, int limit = 50);
    }
}