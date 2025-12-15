using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Core.Services;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using UAParser.Interfaces;

namespace AIArbitration.Infrastructure.Services
{
    public class UserService : IUserService
    {
        private readonly ILogger<UserService> _logger;
        private readonly ConcurrentDictionary<string, ApplicationUser> _users = new();
        private readonly ConcurrentDictionary<string, UserConstraints> _userConstraints = new();
        private readonly ConcurrentDictionary<string, UserSettings> _userSettings = new();
        private readonly ConcurrentDictionary<string, List<ApiKey>> _userApiKeys = new();
        private readonly ConcurrentDictionary<string, UserSession> _sessions = new();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly IUserAgentParser _userAgentParser;

        public UserService(
            ILogger<UserService> logger, 
            IHttpContextAccessor httpContextAccessor,
            HttpClient httpClient,
            IUserAgentParser parser)
        {
            _logger = logger;
            _httpClient = httpClient;
            _userAgentParser = parser;
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClient;
        }

        public async Task<ApplicationUser?> GetUserByIdAsync(string userId)
        {
            _users.TryGetValue(userId, out var user);
            return user;
        }

        public async Task<ApplicationUser?> GetUserByEmailAsync(string email)
        {
            return _users.Values.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<List<ApplicationUser>> GetUsersByTenantAsync(string tenantId)
        {
            return _users.Values.Where(u => u.TenantId == tenantId).ToList();
        }

        public async Task<ApplicationUser> CreateUserAsync(CreateUserCommand command)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = command.TenantId,
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsActive = true
            };

            // Hash password if provided
            if (!string.IsNullOrEmpty(command.Password))
            {
                user.HashedPassword = HashPassword(command.Password);
            }

            _users[user.Id] = user;

            // Create default constraints and settings
            _userConstraints[user.Id] = new UserConstraints
            {
                UserId = user.Id,
                TenantId = command.TenantId,
                MaxRequestsPerDay = 1000,
                AllowedModels = new List<string>(), // Empty means all models allowed
                BlockedModels = new List<string>(),
                DailyCostLimit = 100.00m
            };

            _userSettings[user.Id] = new UserSettings
            {
                UserId = user.Id,
                TenantId = command.TenantId,
                DefaultModel = null,
                DefaultTaskType = "general",
                ReceiveNotifications = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _logger.LogInformation("Created user {UserId} with email {Email}", user.Id, user.Email);

            return user;
        }

        public async Task<ApplicationUser> UpdateUserAsync(UpdateUserCommand command)
        {
            if (!_users.TryGetValue(command.UserId, out var user))
            {
                throw new InvalidOperationException($"User {command.UserId} not found");
            }

            user.FirstName = command.FirstName ?? user.FirstName;
            user.LastName = command.LastName ?? user.LastName;
            user.Email = command.Email ?? user.Email;
            user.UpdatedAt = DateTime.UtcNow;

            // Update password if provided
            if (!string.IsNullOrEmpty(command.Password))
            {
                user.HashedPassword = HashPassword(command.Password);
            }

            _users[command.UserId] = user;
            _logger.LogInformation("Updated user {UserId}", user.Id);

            return user;
        }

        public async Task DeleteUserAsync(string userId)
        {
            _users.TryRemove(userId, out _);
            _userConstraints.TryRemove(userId, out _);
            _userSettings.TryRemove(userId, out _);
            _userApiKeys.TryRemove(userId, out _);

            // Remove all sessions for this user
            var sessionsToRemove = _sessions.Where(kvp => kvp.Value.UserId == userId).ToList();
            foreach (var kvp in sessionsToRemove)
            {
                _sessions.TryRemove(kvp.Key, out _);
            }

            _logger.LogInformation("Deleted user {UserId}", userId);
        }

        public async Task<UserConstraints> GetUserConstraintsAsync(string userId)
        {
            if (!_userConstraints.TryGetValue(userId, out var constraints))
            {
                constraints = new UserConstraints
                {
                    UserId = userId
                };
            }

            return constraints;
        }

        public async Task UpdateUserConstraintsAsync(string userId, UserConstraints constraints)
        {
            constraints.UserId = userId;
            _userConstraints[userId] = constraints;
            _logger.LogInformation("Updated constraints for user {UserId}", userId);
        }

        public async Task<UserSettings> GetUserSettingsAsync(string userId)
        {
            if (!_userSettings.TryGetValue(userId, out var settings))
            {
                settings = new UserSettings
                {
                    UserId = userId
                };
            }

            return settings;
        }

        public async Task UpdateUserSettingsAsync(string userId, UserSettings settings)
        {
            settings.UserId = userId;
            settings.UpdatedAt = DateTime.UtcNow;
            _userSettings[userId] = settings;
            _logger.LogInformation("Updated settings for user {UserId}", userId);
        }

        public async Task<ApiKey> CreateApiKeyAsync(CreateApiKeyCommand command)
        {
            var apiKey = new ApiKey
            {
                Id = Guid.NewGuid().ToString(),
                UserId = command.UserId,
                TenantId = command.TenantId,
                Name = command.Name,
                Key = GenerateApiKey(),
                CreatedAt = DateTime.UtcNow,
                LastUsedAt = null,
                IsActive = true
            };

            if (!_userApiKeys.ContainsKey(command.UserId))
            {
                _userApiKeys[command.UserId] = new List<ApiKey>();
            }

            _userApiKeys[command.UserId].Add(apiKey);
            _logger.LogInformation("Created API key {ApiKeyId} for user {UserId}", apiKey.Id, command.UserId);

            return apiKey;
        }

        public async Task<List<ApiKey>> GetUserApiKeysAsync(string userId, string tenantId)
        {
            if (!_userApiKeys.TryGetValue(userId, out var apiKeys))
            {
                return new List<ApiKey>();
            }

            return apiKeys.Where(k => k.TenantId == tenantId).ToList();
        }

        public async Task RevokeApiKeyAsync(string apiKeyId, string userId, string tenantId)
        {
            if (_userApiKeys.TryGetValue(userId, out var apiKeys))
            {
                var apiKey = apiKeys.FirstOrDefault(k => k.Id == apiKeyId && k.TenantId == tenantId);
                if (apiKey != null)
                {
                    apiKey.IsActive = false;
                    _logger.LogInformation("Revoked API key {ApiKeyId} for user {UserId}", apiKeyId, userId);
                }
            }
        }

        public async Task<ApiKey?> ValidateApiKeyAsync(string apiKey)
        {
            foreach (var userKeys in _userApiKeys.Values)
            {
                var key = userKeys.FirstOrDefault(k => k.Key == apiKey && k.IsActive);
                if (key != null)
                {
                    return key;
                }
            }

            return null;
        }

        public async Task UpdateApiKeyLastUsedAsync(string apiKeyId)
        {
            foreach (var userKeys in _userApiKeys.Values)
            {
                var key = userKeys.FirstOrDefault(k => k.Id == apiKeyId);
                if (key != null)
                {
                    key.LastUsedAt = DateTime.UtcNow;
                    return;
                }
            }
        }

        public async Task<UserSession> CreateSessionAsync(CreateSessionCommand command)
        {
            var session = new UserSession
            {
                Id = Guid.NewGuid().ToString(),
                UserId = command.UserId,
                TenantId = command.TenantId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                IsActive = true
            };

            _sessions[session.Id] = session;
            _logger.LogInformation("Created session {SessionId} for user {UserId}", session.Id, command.UserId);

            return session;
        }

        public async Task<UserSession?> GetSessionAsync(string sessionId)
        {
            _sessions.TryGetValue(sessionId, out var session);

            // Check if session is expired
            if (session != null && session.ExpiresAt < DateTime.UtcNow)
            {
                session.IsActive = false;
                return null;
            }

            return session;
        }

        public async Task UpdateSessionAsync(string sessionId, UpdateSessionCommand command)
        {
            if (!_sessions.TryGetValue(sessionId, out var session))
            {
                throw new InvalidOperationException($"Session {sessionId} not found");
            }

            if (command.ExtendExpiration)
            {
                session.ExpiresAt = DateTime.UtcNow.AddHours(24);
            }

            session.UpdatedAt = DateTime.UtcNow;
            _sessions[sessionId] = session;
        }

        public async Task TerminateSessionAsync(string sessionId, string reason)
        {
            if (_sessions.TryGetValue(sessionId, out var session))
            {
                session.IsActive = false;
                session.TerminatedAt = DateTime.UtcNow;
                session.TerminationReason = reason;
                _logger.LogInformation("Terminated session {SessionId}: {Reason}", sessionId, reason);
            }
        }

        public async Task<UserUsage> GetUserUsageAsync(string userId, string tenantId, UsageQuery query)
        {
            // This would typically call the CostTrackingService
            return new UserUsage
            {
                UserId = userId,
                TenantId = tenantId,
                PeriodStart = query.StartDate,
                PeriodEnd = query.EndDate,
                TotalCost = 50.25m,
                TotalRequests = 250,
                AverageCostPerRequest = 0.201m
            };
        }

        public async Task<List<UserUsage>> GetUsersUsageAsync(string tenantId, UsageQuery query)
        {
            // This would typically call the CostTrackingService
            var users = await GetUsersByTenantAsync(tenantId);
            var usages = new List<UserUsage>();

            foreach (var user in users)
            {
                var usage = await GetUserUsageAsync(user.Id, tenantId, query);
                usages.Add(usage);
            }

            return usages;
        }

        public async Task<UserStatistics> GetUserStatisticsAsync(string userId, string tenantId, DateTime start, DateTime end)
        {
            var usage = await GetUserUsageAsync(userId, tenantId, new UsageQuery { StartDate = start, EndDate = end });
            var apiKeys = await GetUserApiKeysAsync(userId, tenantId);
            var sessions = _sessions.Values.Where(s => s.UserId == userId && s.TenantId == tenantId).ToList();

            return new UserStatistics
            {
                UserId = userId,
                TenantId = tenantId,
                PeriodStart = start,
                PeriodEnd = end,
                GeneratedAt = DateTime.UtcNow,
                TotalCost = usage.TotalCost,
                TotalRequests = usage.TotalRequests,
                ActiveApiKeys = apiKeys.Count(k => k.IsActive),
                ActiveSessions = sessions.Count(s => s.IsActive && s.ExpiresAt > DateTime.UtcNow)
            };
        }

        public async Task<List<BudgetAllocation>> GetUserBudgetsAsync(string userId, string tenantId)
        {
            // This would typically call the BudgetService
            return new List<BudgetAllocation>();
        }

        public async Task<BudgetAllocation?> GetUserBudgetAsync(string userId, string tenantId, string? projectId = null)
        {
            var budgets = await GetUserBudgetsAsync(userId, tenantId);
            return budgets.FirstOrDefault(b => b.ProjectId == projectId);
        }

        public async Task<List<UserPermission>> GetUserPermissionsAsync(string userId)
        {
            // Default permissions
            return new List<UserPermission>
            {
                new() { Id = "read_models", Name = "Read Models", Description = "Can view available models" },
                new() { Id = "execute_requests", Name = "Execute Requests", Description = "Can execute AI model requests" }
            };
        }

        public async Task GrantPermissionAsync(string userId, UserPermission permission)
        {
            _logger.LogInformation("Granted permission {PermissionId} to user {UserId}", permission.Id, userId);
        }

        public async Task RevokePermissionAsync(string userId, string permissionId)
        {
            _logger.LogInformation("Revoked permission {PermissionId} from user {UserId}", permissionId, userId);
        }

        public async Task<AuthenticationResult> AuthenticateAsync(string email, string password)
        {
            var user = await GetUserByEmailAsync(email);
            if (user == null || user.HashedPassword != HashPassword(password))
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid email or password"
                };
            }

            var context = _httpContextAccessor.HttpContext!;
            var userInfo = new GetIpAddressAndUserAgent(_httpContextAccessor, _httpClient, _userAgentParser);
            var userAgent = userInfo.GetUserAgent(context);

            var session = await CreateSessionAsync(new CreateSessionCommand
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                IPAddress = userInfo.GetClientIP(context),
                UserAgent = userAgent["UserAgent"]
            });

            return new AuthenticationResult
            {
                Success = true,
                User = user,
                Session = session
            };
        }

        public async Task<AuthenticationResult> AuthenticateWithApiKeyAsync(string apiKey)
        {
            var key = await ValidateApiKeyAsync(apiKey);
            if (key == null)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "Invalid API key"
                };
            }

            var user = await GetUserByIdAsync(key.UserId);
            if (user == null)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    ErrorMessage = "User not found"
                };
            }

            // Update last used timestamp
            await UpdateApiKeyLastUsedAsync(key.Id);

            var context = _httpContextAccessor.HttpContext;
            string ipAddress = context != null ? new GetIpAddressAndUserAgent(_httpContextAccessor, _httpClient, _userAgentParser).GetClientIP(context) : string.Empty;
            string userAgent = context != null ? new GetIpAddressAndUserAgent(_httpContextAccessor, _httpClient, _userAgentParser).GetUserAgent(context)["UserAgent"] : string.Empty;

            var session = await CreateSessionAsync(new CreateSessionCommand
            {
                UserId = user.Id,
                TenantId = user.TenantId,
                IPAddress = ipAddress,
                UserAgent = userAgent
            });

            return new AuthenticationResult
            {
                Success = true,
                User = user,
                Session = session,
                ApiKey = key
            };
        }

        public async Task LogoutAsync(string userId, string sessionId)
        {
            await TerminateSessionAsync(sessionId, "User logged out");
            _logger.LogInformation("User {UserId} logged out from session {SessionId}", userId, sessionId);
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        private string GenerateApiKey()
        {
            return $"ak_" + Guid.NewGuid().ToString("N").ToLower();
        }
    }
}

