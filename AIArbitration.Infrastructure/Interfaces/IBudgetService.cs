using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IBudgetService
    {
        // Budget management
        Task<BudgetAllocation?> GetBudgetByIdAsync(string budgetId);
        Task<List<BudgetAllocation>> GetBudgetsAsync(string tenantId, string? projectId = null, string? userId = null);
        Task<BudgetAllocation> CreateBudgetAsync(BudgetAllocation budget);
        Task<BudgetAllocation> UpdateBudgetAsync(BudgetAllocation budget);
        Task DeleteBudgetAsync(string budgetId);

        // Budget status and checking
        Task<BudgetStatus> GetBudgetStatusAsync(string tenantId, string? projectId = null, string? userId = null);
        Task<BudgetStatus> GetBudgetStatusAsync(string budgetId);
        Task<bool> CheckBudgetAsync(string tenantId, decimal estimatedCost, string? projectId = null, string? userId = null);
        Task<BudgetCheckResult> CheckBudgetWithDetailsAsync(string tenantId, decimal estimatedCost, string? projectId = null, string? userId = null);

        // Usage tracking
        Task RecordUsageAsync(UsageRecord usage);
        Task RecordUsageAsync(string tenantId, string? projectId, string? userId, decimal amount, string currency = "USD");
        Task<decimal> GetCurrentUsageAsync(string budgetId);
        Task<decimal> GetCurrentUsageAsync(string tenantId, string? projectId = null, string? userId = null);

        // Notifications
        Task<List<BudgetNotification>> GetBudgetNotificationsAsync(string budgetId, DateTime start, DateTime end);
        Task SendBudgetNotificationsAsync();
        Task MarkNotificationAsReadAsync(string notificationId);

        // Forecasting
        Task<BudgetForecast> GetBudgetForecastAsync(string budgetId, int forecastDays = 30);
        Task<BudgetForecast> GetBudgetForecastAsync(string tenantId, string? projectId = null, string? userId = null, int forecastDays = 30);

        // Analysis
        Task<BudgetAnalysis> AnalyzeBudgetUsageAsync(string budgetId, DateTime start, DateTime end);
        Task<List<BudgetAlert>> GetBudgetAlertsAsync(string tenantId, DateTime start, DateTime end);

        // Reset and rollover
        Task ResetBudgetAsync(string budgetId);
        Task<bool> CanRolloverBudgetAsync(string budgetId);
        Task<BudgetAllocation> RolloverBudgetAsync(string budgetId, decimal? newAmount = null);
    }
}
