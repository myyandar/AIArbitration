using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Core.Services;
using AIArbitration.Infrastructure.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IUserService
    {
        //// User management
        //Task<ApplicationUser?> GetUserByIdAsync(string userId);
        //Task<ApplicationUser?> GetUserByEmailAsync(string email);
        //Task<List<ApplicationUser>> GetUsersByTenantAsync(string tenantId);
        //Task<ApplicationUser> CreateUserAsync(CreateUserCommand command);
        //Task<ApplicationUser> UpdateUserAsync(UpdateUserCommand command);
        //Task DeleteUserAsync(string userId);

        //// User constraints and settings
        //Task<UserConstraints> GetUserConstraintsAsync(string userId);
        //Task UpdateUserConstraintsAsync(string userId, UserConstraints constraints);
        //Task<UserSettings> GetUserSettingsAsync(string userId);
        //Task UpdateUserSettingsAsync(string userId, UserSettings settings);

        //// API key management
        //Task<ApiKey> CreateApiKeyAsync(CreateApiKeyCommand command);
        //Task<List<ApiKey>> GetUserApiKeysAsync(string userId, string tenantId);
        //Task RevokeApiKeyAsync(string apiKeyId, string userId, string tenantId);
        //Task<ApiKey?> ValidateApiKeyAsync(string apiKey);
        //Task UpdateApiKeyLastUsedAsync(string apiKeyId);

        //// Sessions
        //Task<UserSession> CreateSessionAsync(CreateSessionCommand command);
        //Task<UserSession?> GetSessionAsync(string sessionId);
        //Task UpdateSessionAsync(string sessionId, UpdateSessionCommand command);
        //Task TerminateSessionAsync(string sessionId, string reason);

        //// Usage and statistics
        //Task<UserUsage> GetUserUsageAsync(string userId, string tenantId, UsageQuery query);
        //Task<List<UserUsage>> GetUsersUsageAsync(string tenantId, UsageQuery query);
        //Task<UserStatistics> GetUserStatisticsAsync(string userId, string tenantId, DateTime start, DateTime end);

        //// Budgets
        //Task<List<BudgetAllocation>> GetUserBudgetsAsync(string userId, string tenantId);
        //Task<BudgetAllocation?> GetUserBudgetAsync(string userId, string tenantId, string? projectId = null);

        //// Permissions
        //Task<List<UserPermission>> GetUserPermissionsAsync(string userId);
        //Task GrantPermissionAsync(string userId, UserPermission permission);
        //Task RevokePermissionAsync(string userId, string permissionId);

        //// Authentication
        //Task<AuthenticationResult> AuthenticateAsync(string email, string password);
        //Task<AuthenticationResult> AuthenticateWithApiKeyAsync(string apiKey);
        //Task LogoutAsync(string userId, string sessionId);


        //////////////////////////////////////////////////////////////////////
        ///

        Task<Core.Entities.ApplicationUser?> GetUserByIdAsync(string userId);
        Task<ApplicationUser?> GetUserByEmailAsync(string email);
        Task<List<ApplicationUser>> GetUsersByTenantAsync(string tenantId);
        Task<ApplicationUser> CreateUserAsync(CreateUserCommand command);
        Task<ApplicationUser> UpdateUserAsync(UpdateUserCommand command);
        Task DeleteUserAsync(string userId);
        Task<UserConstraints> GetUserConstraintsAsync(string userId);
        Task UpdateUserConstraintsAsync(string userId, UserConstraints constraints);
        Task<UserSettings> GetUserSettingsAsync(string userId);
        Task UpdateUserSettingsAsync(string userId, UserSettings settings);
        Task<ApiKey> CreateApiKeyAsync(CreateApiKeyCommand command);
        Task<List<ApiKey>> GetUserApiKeysAsync(string userId, string tenantId);
        Task RevokeApiKeyAsync(string apiKeyId, string userId, string tenantId);
        Task<ApiKey?> ValidateApiKeyAsync(string apiKey);
        Task UpdateApiKeyLastUsedAsync(string apiKeyId);
        Task<UserSession> CreateSessionAsync(CreateSessionCommand command);
        Task<UserSession?> GetSessionAsync(string sessionId);
        Task UpdateSessionAsync(string sessionId, UpdateSessionCommand command);
        Task TerminateSessionAsync(string sessionId, string reason);
        Task<UserUsage> GetUserUsageAsync(string userId, string tenantId, UsageQuery query);
        Task<List<UserUsage>> GetUsersUsageAsync(string tenantId, UsageQuery query);
        Task<UserStatistics> GetUserStatisticsAsync(string userId, string tenantId, DateTime start, DateTime end);
        Task<List<BudgetAllocation>> GetUserBudgetsAsync(string userId, string tenantId);
        Task<BudgetAllocation?> GetUserBudgetAsync(string userId, string tenantId, string? projectId = null);
        Task<List<UserPermission>> GetUserPermissionsAsync(string userId);
        Task GrantPermissionAsync(string userId, UserPermission permission);
        Task RevokePermissionAsync(string userId, string permissionId);
        Task<AuthenticationResult> AuthenticateAsync(string email, string password);
        Task<AuthenticationResult> AuthenticateWithApiKeyAsync(string apiKey);
    }
}
