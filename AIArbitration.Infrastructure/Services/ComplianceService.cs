using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    using AIArbitration.Core;
    using AIArbitration.Core.Entities;
    using AIArbitration.Core.Entities.Enums;
    using AIArbitration.Core.Models;
    using AIArbitration.Infrastructure.Interfaces;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Logging;
    using Soenneker.Extensions.String;
    using System.Text.Json;
    using System.Text.RegularExpressions;

    public class ComplianceService : IComplianceService
    {
        private readonly AIArbitrationDbContext _context;
        private readonly ILogger<ComplianceService> _logger;
        private readonly IRuleEngine _ruleEngine;

        public ComplianceService(
            AIArbitrationDbContext context,
            ILogger<ComplianceService> logger,
            IRuleEngine ruleEngine)
        {
            _context = context;
            _logger = logger;
            _ruleEngine = ruleEngine;
        }

        #region Compliance Checking

        public async Task<ComplianceCheckResult> CheckModelComplianceAsync(AIModel model, ArbitrationContext context)
        {
            try
            {
                // Get applicable rules for this context
                var rules = await GetApplicableRulesAsync(context.TenantId, context);

                var results = new List<ComplianceCheckResult>();
                var isCompliant = true;
                var errorDetails = new List<string>();

                foreach (var rule in rules.Where(r => r.IsEnabled))
                {
                    // Create evaluation context
                    var evalContext = new
                    {
                        Model = new
                        {
                            model.Id,
                            model.Name,
                            model.ProviderId,
                            model.Tier,
                            model.SupportsDataResidency,
                            model.SupportsEncryptionAtRest,
                            model.Provider?.SupportedRegions,
                            model.Capabilities
                        },
                        Context = new
                        {
                            context.TenantId,
                            context.TaskType,
                            context.ExpectedInputTokens,
                            context.ExpectedOutputTokens,
                            context.RequiredRegion,
                            context.RequiresEncryption,
                            context.RequiresFunctionCalling,
                            context.RequiresVision,
                            context.RequiresAudio,
                            context.RequiresStreaming
                        },
                        Timestamp = DateTime.UtcNow
                    };

                    var result = rule.CheckCompliance(evalContext);
                    results.Add(result);

                    if (!result.IsCompliant)
                    {
                        isCompliant = false;
                        errorDetails.Add($"{rule.Name}: {result.Details}");
                    }

                    // Log the compliance check
                    await LogComplianceCheckAsync(new ComplianceLog
                    {
                        Id = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        TenantId = context.TenantId,
                        Result = result,
                        ResourceType = "model",
                        ResourceId = model.Id,
                        UserId = context.UserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                return new ComplianceCheckResult
                {
                    IsCompliant = isCompliant,
                    RuleId = isCompliant ? "all_rules" : "multiple_rules",
                    RuleName = isCompliant ? "All Rules" : "Multiple Rules",
                    Timestamp = DateTime.UtcNow,
                    Details = isCompliant ? "Model meets all compliance requirements" : string.Join("; ", errorDetails),
                    Metadata = new Dictionary<string, string>
                    {
                        ["model_id"] = model.Id,
                        ["provider_id"] = model.ProviderId,
                        ["rules_evaluated"] = rules.Count(r => r.IsEnabled).ToString(),
                        ["rules_passed"] = results.Count(r => r.IsCompliant).ToString(),
                        ["rules_failed"] = results.Count(r => !r.IsCompliant).ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking model compliance for model {ModelId}", model?.Id);

                return new ComplianceCheckResult
                {
                    IsCompliant = false,
                    RuleId = "error",
                    RuleName = "System Error",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message,
                    Details = "Failed to evaluate compliance due to system error"
                };
            }
        }

        public async Task<ComplianceCheckResult> CheckRequestComplianceAsync(ChatRequest request, ArbitrationContext context)
        {
            try
            {
                var rules = await GetApplicableRulesAsync(request.TenantId, context);

                var results = new List<ComplianceCheckResult>();
                var isCompliant = true;
                var errorDetails = new List<string>();

                // Check for sensitive data
                var sensitiveDataDetected = await DetectSensitiveDataAsync(request.Messages);

                foreach (var rule in rules.Where(r => r.IsEnabled))
                {
                    var evalContext = new
                    {
                        Request = new
                        {
                            request.Id,
                            request.UserId,
                            request.ProjectId,
                            request.ModelId,
                            request.Purpose,
                            request.Region,
                            request.RequiresEncryption,
                            request.Consents,
                            SensitiveDataDetected = sensitiveDataDetected,
                            MessageCount = request.Messages.Count,
                            HasPersonalData = await ContainsPersonalDataAsync(request.Messages)
                        },
                        Context = context,
                        Timestamp = DateTime.UtcNow
                    };

                    var result = rule.CheckCompliance(evalContext);
                    results.Add(result);

                    if (!result.IsCompliant)
                    {
                        isCompliant = false;
                        errorDetails.Add($"{rule.Name}: {result.Details}");
                    }

                    await LogComplianceCheckAsync(new ComplianceLog
                    {
                        Id = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        TenantId = request.TenantId,
                        Result = result,
                        ResourceType = "request",
                        ResourceId = request.Id,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                return new ComplianceCheckResult
                {
                    IsCompliant = isCompliant,
                    RuleId = isCompliant ? "all_rules" : "multiple_rules",
                    RuleName = isCompliant ? "All Rules" : "Multiple Rules",
                    Timestamp = DateTime.UtcNow,
                    Details = isCompliant ? "Request meets all compliance requirements" : string.Join("; ", errorDetails),
                    Metadata = new Dictionary<string, string>
                    {
                        ["request_id"] = request.Id,
                        ["user_id"] = request.UserId,
                        ["has_sensitive_data"] = sensitiveDataDetected.Any().ToString(),
                        ["sensitive_data_types"] = sensitiveDataDetected.ToString(),
                        ["rules_evaluated"] = rules.Count(r => r.IsEnabled).ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking request compliance for request {RequestId}", request?.Id);

                return new ComplianceCheckResult
                {
                    IsCompliant = false,
                    RuleId = "error",
                    RuleName = "System Error",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message,
                    Details = "Failed to evaluate compliance due to system error"
                };
            }
        }

        public async Task<ComplianceCheckResult> CheckResponseComplianceAsync(ModelResponse response, ArbitrationContext context)
        {
            try
            {
                // Get the associated request
                var request = await _context.ChatRequests
                    .FirstOrDefaultAsync(r => r.Id == response.RequestId);

                if (request == null)
                    throw new ArgumentException($"Request {response.RequestId} not found");

                var rules = await GetApplicableRulesAsync(request.TenantId, context);

                var results = new List<ComplianceCheckResult>();
                var isCompliant = true;
                var errorDetails = new List<string>();

                // Check response content
                var contentAnalysis = await AnalyzeResponseContentAsync(response.Content);

                foreach (var rule in rules.Where(r => r.IsEnabled))
                {
                    var evalContext = new
                    {
                        Response = new
                        {
                            response.Id,
                            response.ModelId,
                            response.ProviderId,
                            response.InputTokens,
                            response.OutputTokens,
                            response.Cost,
                            response.Latency,
                            response.IsSuccess,
                            response.Flags,
                            ContentAnalysis = contentAnalysis
                        },
                        Request = new
                        {
                            request.Id,
                            request.UserId,
                            request.TenantId,
                            request.Purpose
                        },
                        Context = context,
                        Timestamp = DateTime.UtcNow
                    };

                    var result = rule.CheckCompliance(evalContext);
                    results.Add(result);

                    if (!result.IsCompliant)
                    {
                        isCompliant = false;
                        errorDetails.Add($"{rule.Name}: {result.Details}");
                    }

                    await LogComplianceCheckAsync(new ComplianceLog
                    {
                        Id = Guid.NewGuid().ToString(),
                        RuleId = rule.Id,
                        TenantId = request.TenantId,
                        Result = result,
                        ResourceType = "response",
                        ResourceId = response.Id,
                        UserId = request.UserId,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                // Create audit trail for response
                await CreateAuditTrailAsync(request.TenantId, "response_received", "response",
                    response.Id, request.UserId, new Dictionary<string, object>
                    {
                        ["model_id"] = response.ModelId,
                        ["success"] = response.IsSuccess,
                        ["latency_ms"] = response.Latency.TotalMilliseconds,
                        ["compliance_check_passed"] = isCompliant
                    });

                return new ComplianceCheckResult
                {
                    IsCompliant = isCompliant,
                    RuleId = isCompliant ? "all_rules" : "multiple_rules",
                    RuleName = isCompliant ? "All Rules" : "Multiple Rules",
                    Timestamp = DateTime.UtcNow,
                    Details = isCompliant ? "Response meets all compliance requirements" : string.Join("; ", errorDetails),
                    Metadata = new Dictionary<string, string>
                    {
                        ["response_id"] = response.Id,
                        ["has_content_issues"] = contentAnalysis.ContainsKey("issues").ToString(),
                        ["content_analysis"] = contentAnalysis.Values.ToString(),
                        ["rules_evaluated"] = rules.Count(r => r.IsEnabled).ToString()
                    }
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking response compliance for response {ResponseId}", response?.Id);

                return new ComplianceCheckResult
                {
                    IsCompliant = false,
                    RuleId = "error",
                    RuleName = "System Error",
                    Timestamp = DateTime.UtcNow,
                    Error = ex.Message,
                    Details = "Failed to evaluate compliance due to system error"
                };
            }
        }

        #endregion

        #region Batch Compliance Checks

        public async Task<List<ComplianceCheckResult>> CheckModelsComplianceAsync(List<AIModel> models, ArbitrationContext context)
        {
            try
            {
                var tasks = models.Select(model => CheckModelComplianceAsync(model, context));
                var results = await Task.WhenAll(tasks);
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking batch model compliance");
                throw;
            }
        }

        public async Task<List<ComplianceCheckResult>> CheckRequestsComplianceAsync(List<ChatRequest> requests, ArbitrationContext context)
        {
            try
            {
                var tasks = requests.Select(request => CheckRequestComplianceAsync(request, context));
                var results = await Task.WhenAll(tasks);
                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking batch request compliance");
                throw;
            }
        }

        #endregion

        #region Rule Management

        public async Task<List<ComplianceRule>> GetComplianceRulesAsync(string tenantId)
        {
            try
            {
                return await _context.ComplianceRules
                    .Where(r => r.TenantId == tenantId)
                    .OrderBy(r => r.Priority)
                    .ThenBy(r => r.Name)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance rules for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<ComplianceRule> GetComplianceRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _context.ComplianceRules
                    .Include(r => r.Tenant)
                    .FirstOrDefaultAsync(r => r.Id == ruleId);

                if (rule == null)
                    throw new ArgumentException($"Compliance rule {ruleId} not found");

                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance rule {RuleId}", ruleId);
                throw;
            }
        }

        public async Task<ComplianceRule> CreateComplianceRuleAsync(ComplianceRule rule)
        {
            try
            {
                rule.Id = Guid.NewGuid().ToString();
                rule.CreatedAt = DateTime.UtcNow;
                rule.UpdatedAt = DateTime.UtcNow;

                // Validate rule configuration
                ValidateComplianceRule(rule);

                _context.ComplianceRules.Add(rule);
                await _context.SaveChangesAsync();

                // Create audit trail
                await CreateAuditTrailAsync(rule.TenantId, "rule_created", "compliance_rule",
                    rule.Id, rule.CreatedBy, new Dictionary<string, object>
                    {
                        ["rule_name"] = rule.Name,
                        ["rule_type"] = rule.RuleType.ToString(),
                        ["standard"] = rule.Standard.ToString()
                    });

                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating compliance rule");
                throw;
            }
        }

        public async Task<ComplianceRule> UpdateComplianceRuleAsync(ComplianceRule rule)
        {
            try
            {
                var existingRule = await _context.ComplianceRules
                    .FirstOrDefaultAsync(r => r.Id == rule.Id);

                if (existingRule == null)
                    throw new ArgumentException($"Compliance rule {rule.Id} not found");

                // Validate rule configuration
                ValidateComplianceRule(rule);

                // Preserve creation data
                rule.CreatedAt = existingRule.CreatedAt;
                rule.CreatedBy = existingRule.CreatedBy;
                rule.UpdatedAt = DateTime.UtcNow;

                _context.Entry(existingRule).CurrentValues.SetValues(rule);
                await _context.SaveChangesAsync();

                // Create audit trail
                await CreateAuditTrailAsync(rule.TenantId, "rule_updated", "compliance_rule",
                    rule.Id, rule.UpdatedBy, new Dictionary<string, object>
                    {
                        ["rule_name"] = rule.Name,
                        ["changes"] = GetChangedProperties(existingRule, rule)
                    });

                return rule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating compliance rule {RuleId}", rule.Id);
                throw;
            }
        }

        public async Task DeleteComplianceRuleAsync(string ruleId)
        {
            try
            {
                var rule = await _context.ComplianceRules
                    .FirstOrDefaultAsync(r => r.Id == ruleId);

                if (rule == null)
                    throw new ArgumentException($"Compliance rule {ruleId} not found");

                // Check if rule is mandatory
                if (rule.IsMandatory)
                    throw new InvalidOperationException($"Cannot delete mandatory rule: {rule.Name}");

                // Create audit trail before deletion
                await CreateAuditTrailAsync(rule.TenantId, "rule_deleted", "compliance_rule",
                    rule.Id, "system", new Dictionary<string, object>
                    {
                        ["rule_name"] = rule.Name,
                        ["rule_type"] = rule.RuleType.ToString()
                    });

                _context.ComplianceRules.Remove(rule);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting compliance rule {RuleId}", ruleId);
                throw;
            }
        }

        #endregion

        #region Compliance Validation

        public async Task<ComplianceValidationResult> ValidateTenantComplianceAsync(string tenantId)
        {
            try
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null)
                    throw new ArgumentException($"Tenant {tenantId} not found");

                var rules = await GetComplianceRulesAsync(tenantId);
                var checkResults = new List<ComplianceCheckResult>();
                var violations = new List<string>();
                var warnings = new List<string>();

                // Check each rule
                foreach (var rule in rules.Where(r => r.IsEnabled))
                {
                    var context = new ArbitrationContext { TenantId = tenantId };
                    var evalContext = new { Tenant = tenant, Timestamp = DateTime.UtcNow };

                    var result = rule.CheckCompliance(evalContext);
                    checkResults.Add(result);

                    if (!result.IsCompliant)
                    {
                        violations.Add($"{rule.Name}: {result.Details}");
                    }
                    else if (rule.EnforcementSeverity == EnforcementSeverity.Warning)
                    {
                        warnings.Add($"{rule.Name}: {result.Details}");
                    }
                }

                // Check user consents
                var consentCheck = await ValidateUserConsentsAsync(tenantId);
                if (!consentCheck.IsCompliant)
                {
                    violations.Add($"Consent validation failed: {consentCheck.Details}");
                    checkResults.Add(consentCheck);
                }

                var isCompliant = !violations.Any() && consentCheck.IsCompliant;

                return new ComplianceValidationResult
                {
                    EntityId = tenantId,
                    EntityType = "tenant",
                    IsCompliant = isCompliant,
                    CheckResults = checkResults,
                    Violations = violations,
                    Warnings = warnings,
                    Metadata = new Dictionary<string, object>
                    {
                        ["total_rules"] = rules.Count,
                        ["enabled_rules"] = rules.Count(r => r.IsEnabled),
                        ["compliant_rules"] = checkResults.Count(r => r.IsCompliant),
                        ["non_compliant_rules"] = checkResults.Count(r => !r.IsCompliant),
                        ["validation_date"] = DateTime.UtcNow
                    },
                    ValidatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating tenant compliance for {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<ComplianceValidationResult> ValidateProjectComplianceAsync(string projectId)
        {
            try
            {
                var project = await _context.Projects
                    .Include(p => p.Tenant)
                    .FirstOrDefaultAsync(p => p.Id == projectId);

                if (project == null)
                    throw new ArgumentException($"Project {projectId} not found");

                var tenantResult = await ValidateTenantComplianceAsync(project.TenantId);

                // Add project-specific checks
                var projectRules = await GetApplicableRulesAsync(project.TenantId,
                    new ArbitrationContext { TenantId = project.TenantId, ProjectId = projectId });

                var checkResults = tenantResult.CheckResults.ToList();
                var violations = tenantResult.Violations.ToList();
                var warnings = tenantResult.Warnings.ToList();

                foreach (var rule in projectRules.Where(r => r.IsEnabled && r.Scope == ComplianceRuleScope.Project))
                {
                    var evalContext = new { Project = project, Tenant = project.Tenant, Timestamp = DateTime.UtcNow };
                    var result = rule.CheckCompliance(evalContext);
                    checkResults.Add(result);

                    if (!result.IsCompliant)
                    {
                        violations.Add($"{rule.Name}: {result.Details}");
                    }
                }

                var isCompliant = !violations.Any();

                return new ComplianceValidationResult
                {
                    EntityId = projectId,
                    EntityType = "project",
                    IsCompliant = isCompliant,
                    CheckResults = checkResults,
                    Violations = violations,
                    Warnings = warnings,
                    Metadata = new Dictionary<string, object>
                    {
                        ["tenant_id"] = project.TenantId,
                        ["project_name"] = project.Name,
                        ["project_specific_rules"] = projectRules.Count(r => r.Scope == ComplianceRuleScope.Project)
                    },
                    ValidatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating project compliance for {ProjectId}", projectId);
                throw;
            }
        }

        public async Task<ComplianceValidationResult> ValidateUserComplianceAsync(string userId)
        {
            try
            {
                var user = await _context.ApplicationUsers
                    .Include(u => u.ArbitrationDecisions)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    throw new ArgumentException($"User {userId} not found");

                // Get user's projects and tenants
                var userProjects = await _context.ArbitrationDecisions
                    .Where(d => d.UserId == userId)
                    .Select(d => d.ProjectId)
                    .Distinct()
                    .ToListAsync();

                var tenantIds = await _context.ArbitrationDecisions
                    .Where(d => d.UserId == userId)
                    .Select(d => d.TenantId)
                    .Distinct()
                    .ToListAsync();

                var checkResults = new List<ComplianceCheckResult>();
                var violations = new List<string>();
                var warnings = new List<string>();

                // Check user consents
                var userConsents = await _context.ConsentRecords
                    .Where(c => c.UserId == userId)
                    .ToListAsync();

                var requiredConsents = new[]
                {
                "privacy_policy",
                "terms_of_service",
                "data_processing"
            };

                foreach (var consentType in requiredConsents)
                {
                    var latestConsent = userConsents
                        .Where(c => c.ConsentType == consentType && c.Given)
                        .OrderByDescending(c => c.GivenAt)
                        .FirstOrDefault();

                    if (latestConsent == null)
                    {
                        violations.Add($"Missing required consent: {consentType}");
                    }
                    else if (DateTime.UtcNow - latestConsent.GivenAt > TimeSpan.FromDays(365))
                    {
                        warnings.Add($"Consent for {consentType} is over 1 year old");
                    }
                }

                var isCompliant = !violations.Any();

                return new ComplianceValidationResult
                {
                    EntityId = userId,
                    EntityType = "user",
                    IsCompliant = isCompliant,
                    CheckResults = checkResults,
                    Violations = violations,
                    Warnings = warnings,
                    Metadata = new Dictionary<string, object>
                    {
                        ["email"] = user.Email,
                        ["active_consents"] = userConsents.Count(c => c.Given && c.RevokedAt == null),
                        ["tenants_used"] = tenantIds.Count,
                        ["projects_used"] = userProjects.Count
                    },
                    ValidatedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating user compliance for {UserId}", userId);
                throw;
            }
        }

        #endregion

        #region Logging and Auditing

        public async Task LogComplianceCheckAsync(ComplianceLog log)
        {
            try
            {
                log.Id = Guid.NewGuid().ToString();
                log.CreatedAt = DateTime.UtcNow;

                _context.ComplianceLogs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging compliance check");
                // Don't throw - logging failures shouldn't break the main flow
            }
        }

        public async Task<List<ComplianceLog>> GetComplianceLogsAsync(string tenantId, DateTime start, DateTime end)
        {
            try
            {
                return await _context.ComplianceLogs
                    .Include(l => l.Rule)
                    .Where(l => l.TenantId == tenantId && l.CreatedAt >= start && l.CreatedAt <= end)
                    .OrderByDescending(l => l.CreatedAt)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance logs for tenant {TenantId}", tenantId);
                throw;
            }
        }

        #endregion

        #region Data Handling

        public async Task<DataHandlingResult> HandleDataRequestAsync(DataRequest request)
        {
            try
            {
                request.Id = Guid.NewGuid().ToString();
                request.RequestedAt = DateTime.UtcNow;
                request.Status = "processing";

                _context.DataRequests.Add(request);
                await _context.SaveChangesAsync();

                // Process based on request type
                DataHandlingResult result;
                switch (request.Type)
                {
                    case DataRequestType.Access:
                        result = await HandleAccessRequestAsync(request);
                        break;
                    case DataRequestType.Portability:
                        result = await HandlePortabilityRequestAsync(request);
                        break;
                    case DataRequestType.Correction:
                        result = await HandleCorrectionRequestAsync(request);
                        break;
                    case DataRequestType.Deletion:
                        result = await HandleDeletionRequestAsync(request);
                        break;
                    default:
                        throw new NotSupportedException($"Data request type {request.Type} not supported");
                }

                // Update request status
                request.Status = result.Success ? "completed" : "failed";
                await _context.SaveChangesAsync();

                // Create audit trail
                await CreateAuditTrailAsync(request.TenantId, "data_request_processed", "data_request",
                    request.Id, request.UserId, new Dictionary<string, object>
                    {
                        ["request_type"] = request.Type.ToString(),
                        ["success"] = result.Success,
                        ["records_affected"] = result.RecordsAffected
                    });

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling data request for user {UserId}", request.UserId);

                return new DataHandlingResult
                {
                    RequestId = request.Id,
                    Type = request.Type,
                    Success = false,
                    ErrorMessage = ex.Message,
                    CompletedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<DataDeletionResult> DeleteUserDataAsync(string userId)
        {
            try
            {
                var user = await _context.ApplicationUsers
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    throw new ArgumentException($"User {userId} not found");

                var recordsDeleted = 0;
                var tablesAffected = new List<string>();

                // Delete in order to respect foreign key constraints
                // 1. Arbitration decisions
                var decisions = await _context.ArbitrationDecisions
                    .Where(d => d.UserId == userId)
                    .ToListAsync();
                _context.ArbitrationDecisions.RemoveRange(decisions);
                recordsDeleted += decisions.Count;
                tablesAffected.Add("ArbitrationDecisions");

                // 2. Consent records
                var consents = await _context.ConsentRecords
                    .Where(c => c.UserId == userId)
                    .ToListAsync();
                _context.ConsentRecords.RemoveRange(consents);
                recordsDeleted += consents.Count;
                tablesAffected.Add("ConsentRecords");

                // 3. Chat requests (anonymize instead of delete for audit trail)
                var requests = await _context.ChatRequests
                    .Where(r => r.UserId == userId)
                    .ToListAsync();
                foreach (var request in requests)
                {
                    request.UserId = null;
                    request.Messages = new List<ChatMessage>(); // Clear content
                }
                recordsDeleted += requests.Count;
                tablesAffected.Add("ChatRequests");

                // 4. User account (soft delete)
                user.IsActive = false;
                user.Email = $"deleted_{userId}@example.com";
                user.UserName = "Deleted User";

                await _context.SaveChangesAsync();

                // Create audit trail
                await CreateAuditTrailAsync(null, "user_data_deleted", "user",
                    userId, "system", new Dictionary<string, object>
                    {
                        ["records_deleted"] = recordsDeleted,
                        ["tables_affected"] = tablesAffected,
                        ["soft_deleted"] = true
                    });

                return new DataDeletionResult
                {
                    UserId = userId,
                    Success = true,
                    RecordsDeleted = recordsDeleted,
                    TablesAffected = tablesAffected,
                    DeletedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user data for {UserId}", userId);

                return new DataDeletionResult
                {
                    UserId = userId,
                    Success = false,
                    ErrorMessage = ex.Message,
                    DeletedAt = DateTime.UtcNow
                };
            }
        }

        public async Task<DataExportResult> ExportUserDataAsync(string userId)
        {
            try
            {
                var user = await _context.ApplicationUsers
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user == null)
                    throw new ArgumentException($"User {userId} not found");

                var exportData = new Dictionary<string, object>
                {
                    ["user_info"] = new
                    {
                        user.Id,
                        user.Email,
                        user.UserName,
                        user.IsActive
                    },
                    ["consents"] = await _context.ConsentRecords
                        .Where(c => c.UserId == userId)
                        .Select(c => new
                        {
                            c.ConsentType,
                            c.Version,
                            c.Given,
                            c.GivenAt,
                            c.RevokedAt
                        })
                        .ToListAsync(),
                    ["requests"] = await _context.ChatRequests
                        .Where(r => r.UserId == userId)
                        .Select(r => new
                        {
                            r.Id,
                            r.ModelId,
                            r.RequestedAt,
                            r.Purpose,
                            MessageCount = r.Messages.Count
                        })
                        .ToListAsync(),
                    ["arbitration_decisions"] = await _context.ArbitrationDecisions
                        .Where(d => d.UserId == userId)
                        .Select(d => new
                        {
                            d.Id,
                            d.TenantId,
                            d.ProjectId,
                            d.SelectedModelId,
                            d.TaskType,
                            d.Timestamp
                        })
                        .ToListAsync()
                };

                var recordsByType = new Dictionary<string, int>
                {
                    ["consents"] = exportData["consents"].GetType().GetProperty("Count") == null ? 0 : 1,
                    ["requests"] = ((List<object>)exportData["requests"]).Count,
                    ["decisions"] = ((List<object>)exportData["arbitration_decisions"]).Count
                };

                var totalRecords = recordsByType.Values.Sum();

                // Generate export file (in production, this would save to a file or blob storage)
                var exportJson = JsonSerializer.Serialize(exportData, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                var fileName = $"user_export_{userId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

                // Create audit trail
                await CreateAuditTrailAsync(null, "user_data_exported", "user",
                    userId, "system", new Dictionary<string, object>
                    {
                        ["total_records"] = totalRecords,
                        ["records_by_type"] = recordsByType,
                        ["file_name"] = fileName
                    });

                return new DataExportResult
                {
                    UserId = userId,
                    Success = true,
                    ExportFilePath = $"/exports/{fileName}",
                    TotalRecords = totalRecords,
                    RecordsByType = recordsByType,
                    ExportedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting user data for {UserId}", userId);

                return new DataExportResult
                {
                    UserId = userId,
                    Success = false,
                    ErrorMessage = ex.Message,
                    ExportedAt = DateTime.UtcNow
                };
            }
        }

        #endregion

        #region Compliance Reporting

        public async Task<ComplianceReport> GenerateComplianceReportAsync(string tenantId, DateTime start, DateTime end)
        {
            try
            {
                var tenant = await _context.Tenants
                    .FirstOrDefaultAsync(t => t.Id == tenantId);

                if (tenant == null)
                    throw new ArgumentException($"Tenant {tenantId} not found");

                // Get all rules for this tenant
                var rules = await GetComplianceRulesAsync(tenantId);
                var enabledRules = rules.Where(r => r.IsEnabled).ToList();

                // Get compliance logs for the period
                var logs = await GetComplianceLogsAsync(tenantId, start, end);

                // Calculate scores by category
                var categoryScores = CalculateCategoryScores(rules, logs);

                // Get violations
                var violations = await GetComplianceViolationsAsync(tenantId, start, end);

                // Calculate overall score
                var overallScore = categoryScores.Any()
                    ? categoryScores.Average(c => c.Score)
                    : 100; // Default to 100% if no rules

                // Identify improvements needed
                var improvements = IdentifyImprovements(categoryScores, violations);

                return new ComplianceReport
                {
                    TenantId = tenantId,
                    ReportPeriodStart = start,
                    ReportPeriodEnd = end,
                    PrimaryStandard = ComplianceStandard.GDPR, // This could be configurable
                    OverallComplianceScore = overallScore,
                    CategoryScores = categoryScores,
                    Violations = violations,
                    Improvements = improvements,
                    Metrics = new Dictionary<string, object>
                    {
                        ["total_checks"] = logs.Count,
                        ["passed_checks"] = logs.Count(l => l.Result.IsCompliant),
                        ["failed_checks"] = logs.Count(l => !l.Result.IsCompliant),
                        ["total_rules"] = rules.Count,
                        ["enabled_rules"] = enabledRules.Count,
                        ["compliance_rate"] = logs.Count > 0
                            ? (decimal)logs.Count(l => l.Result.IsCompliant) / logs.Count * 100
                            : 100
                    },
                    GeneratedAt = DateTime.UtcNow
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating compliance report for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task<List<ComplianceViolation>> GetComplianceViolationsAsync(string tenantId, DateTime start, DateTime end)
        {
            try
            {
                var logs = await GetComplianceLogsAsync(tenantId, start, end);
                var violations = new List<ComplianceViolation>();

                foreach (var log in logs.Where(l => !l.Result.IsCompliant))
                {
                    var rule = await GetComplianceRuleAsync(log.RuleId);

                    violations.Add(new ComplianceViolation
                    {
                        ViolationId = Guid.NewGuid().ToString(),
                        TenantId = tenantId,
                        Standard = rule.Standard,
                        RuleId = rule.Id,
                        RuleName = rule.Name,
                        Description = log.Result.Details ?? "Compliance violation",
                        Severity = rule.EnforcementSeverity switch
                        {
                            EnforcementSeverity.Informational => ViolationSeverity.Low,
                            EnforcementSeverity.Warning => ViolationSeverity.Low,
                            EnforcementSeverity.Medium => ViolationSeverity.Medium,
                            EnforcementSeverity.High => ViolationSeverity.High,
                            EnforcementSeverity.Critical => ViolationSeverity.Critical,
                            _ => ViolationSeverity.Medium
                        },
                        EntityId = log.ResourceId,
                        EntityType = log.ResourceType,
                        DetectedAt = log.CreatedAt,
                        IsResolved = false
                    });
                }

                return violations;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance violations for tenant {TenantId}", tenantId);
                throw;
            }
        }

        #endregion

        #region Configuration

        public async Task<ComplianceConfiguration> GetComplianceConfigurationAsync(string tenantId)
        {
            try
            {
                var config = await _context.ComplianceConfigurations
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId);

                if (config == null)
                {
                    // Return default configuration
                    config = new ComplianceConfiguration
                    {
                        TenantId = tenantId,
                        EnabledStandards = new List<ComplianceStandard> { ComplianceStandard.GDPR },
                        DefaultDataRegion = "eu-west-1",
                        EnableAuditTrail = true,
                        AuditRetentionDays = 730,
                        EnableDataEncryption = true,
                        RequireConsent = true,
                        ConsentVersion = "1.0",
                        UpdatedAt = DateTime.UtcNow
                    };
                }

                return config;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting compliance configuration for tenant {TenantId}", tenantId);
                throw;
            }
        }

        public async Task UpdateComplianceConfigurationAsync(string tenantId, ComplianceConfiguration configuration)
        {
            try
            {
                var existingConfig = await _context.ComplianceConfigurations
                    .FirstOrDefaultAsync(c => c.TenantId == tenantId);

                if (existingConfig == null)
                {
                    configuration.TenantId = tenantId;
                    configuration.UpdatedAt = DateTime.UtcNow;
                    _context.ComplianceConfigurations.Add(configuration);
                }
                else
                {
                    configuration.UpdatedAt = DateTime.UtcNow;
                    _context.Entry(existingConfig).CurrentValues.SetValues(configuration);
                }

                await _context.SaveChangesAsync();

                // Create audit trail
                await CreateAuditTrailAsync(tenantId, "configuration_updated", "compliance_configuration",
                    tenantId, "system", new Dictionary<string, object>
                    {
                        ["enabled_standards"] = configuration.EnabledStandards,
                        ["data_region"] = configuration.DefaultDataRegion,
                        ["audit_enabled"] = configuration.EnableAuditTrail
                    });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating compliance configuration for tenant {TenantId}", tenantId);
                throw;
            }
        }

        #endregion

        #region Private Helper Methods

        private async Task<List<ComplianceRule>> GetApplicableRulesAsync(string tenantId, ArbitrationContext context)
        {
            var rules = await _context.ComplianceRules
                .Where(r => r.TenantId == tenantId && r.IsEnabled)
                .ToListAsync();

            return rules.Where(rule => IsRuleApplicable(rule, context)).ToList();
        }

        private bool IsRuleApplicable(ComplianceRule rule, ArbitrationContext context)
        {
            // Check scope
            if (rule.Scope == ComplianceRuleScope.Global)
                return true;

            if (rule.Scope == ComplianceRuleScope.Tenant)
                return true; // All tenant rules apply

            if (rule.Scope == ComplianceRuleScope.Project && !string.IsNullOrEmpty(context.ProjectId))
            {
                if (rule.AppliedToResources != null || !rule.AppliedToResources.Any())
                {
                    Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Join(",", rule.AppliedToResources)));
                    var appliedToProjects = rule.AppliedToUsers != null
                        ? JsonSerializer.Deserialize<List<string>>(stream) ?? new List<string>()
                        : new List<string>();
                    return appliedToProjects.Contains(context.ProjectId);
                }
            }

            if (rule.Scope == ComplianceRuleScope.User && !string.IsNullOrEmpty(context.UserId))
            {
                if (rule.AppliedToUsers != null || !rule.AppliedToUsers.Any())
                {
                    Stream stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Join(",", rule.AppliedToUsers)));
                    var appliedToUsers = rule.AppliedToUsers != null
                        ? JsonSerializer.Deserialize<List<string>>(stream) ?? new List<string>()
                        : new List<string>();
                    return appliedToUsers.Contains(context.UserId);
                }
            }
            return false;
        }

        private async Task<List<string>> DetectSensitiveDataAsync(List<ChatMessage> messages)
        {
            var sensitiveDataTypes = new List<string>();
            var patterns = new Dictionary<string, string>
            {
                ["email"] = @"[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}",
                ["phone"] = @"(\+\d{1,3}[-.]?)?\d{3}[-.]?\d{3}[-.]?\d{4}",
                ["ssn"] = @"\d{3}-\d{2}-\d{4}",
                ["credit_card"] = @"\d{4}[ -]?\d{4}[ -]?\d{4}[ -]?\d{4}",
                ["ip_address"] = @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b"
            };

            foreach (var message in messages)
            {
                foreach (var pattern in patterns)
                {
                    if (Regex.IsMatch(message.Content, pattern.Value))
                    {
                        if (!sensitiveDataTypes.Contains(pattern.Key))
                            sensitiveDataTypes.Add(pattern.Key);
                    }
                }
            }

            return await Task.FromResult(sensitiveDataTypes);
        }

        private async Task<bool> ContainsPersonalDataAsync(List<ChatMessage> messages)
        {
            var personalDataPatterns = new[]
            {
            @"\b(name|address|phone|email|birth|age|gender)\b",
            @"\b(medical|health|diagnosis|treatment)\b",
            @"\b(financial|bank|account|salary|income)\b",
            @"\b(passport|id|driver.?license|national.?id)\b"
        };

            foreach (var message in messages)
            {
                foreach (var pattern in personalDataPatterns)
                {
                    if (Regex.IsMatch(message.Content, pattern, RegexOptions.IgnoreCase))
                        return await Task.FromResult(true);
                }
            }

            return await Task.FromResult(false);
        }

        private async Task<Dictionary<string, object>> AnalyzeResponseContentAsync(string content)
        {
            var analysis = new Dictionary<string, object>();

            // Check for inappropriate content
            var inappropriatePatterns = new[]
            {
            @"\b(hate|violence|harassment|discrimination)\b",
            @"\b(illegal|unethical|dangerous|harmful)\b",
            @"\b(confidential|secret|proprietary)\b"
        };

            var flags = new List<string>();
            foreach (var pattern in inappropriatePatterns)
            {
                if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                    flags.Add("content_filter");
            }

            if (flags.Any())
                analysis["flags"] = flags;

            // Calculate metrics
            analysis["word_count"] = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            analysis["character_count"] = content.Length;
            analysis["has_questions"] = content.Contains('?');
            analysis["has_code"] = content.Contains("```") || content.Contains("def ") || content.Contains("function ");

            return await Task.FromResult(analysis);
        }

        private async Task<ComplianceCheckResult> ValidateUserConsentsAsync(string tenantId)
        {
            // Check if any users are missing required consents
            var usersMissingConsents = await _context.ApplicationUsers
                .Where(u => u.IsActive)
                .Select(u => new
                {
                    UserId = u.Id,
                    HasPrivacyPolicy = _context.ConsentRecords
                        .Any(c => c.UserId == u.Id && c.ConsentType == "privacy_policy" && c.Given),
                    HasTermsOfService = _context.ConsentRecords
                        .Any(c => c.UserId == u.Id && c.ConsentType == "terms_of_service" && c.Given)
                })
                .Where(u => !u.HasPrivacyPolicy || !u.HasTermsOfService)
                .ToListAsync();

            if (usersMissingConsents.Any())
            {
                return new ComplianceCheckResult
                {
                    IsCompliant = false,
                    RuleId = "consent_validation",
                    RuleName = "User Consent Validation",
                    Timestamp = DateTime.UtcNow,
                    Details = $"{usersMissingConsents.Count} users missing required consents",
                    Metadata = new Dictionary<string, string>
                    {
                        ["users_affected"] = usersMissingConsents.Count.ToString(),
                        ["tenant_id"] = tenantId
                    }
                };
            }

            return new ComplianceCheckResult
            {
                IsCompliant = true,
                RuleId = "consent_validation",
                RuleName = "User Consent Validation",
                Timestamp = DateTime.UtcNow,
                Details = "All users have required consents"
            };
        }

        private void ValidateComplianceRule(ComplianceRule rule)
        {
            if (string.IsNullOrWhiteSpace(rule.Name))
                throw new ArgumentException("Rule name is required");

            if (string.IsNullOrWhiteSpace(rule.Condition))
                throw new ArgumentException("Rule condition is required");

            if (string.IsNullOrWhiteSpace(rule.Action))
                throw new ArgumentException("Rule action is required");

            // Validate JSON fields if they exist
            if (rule.AppliedToResources != null || rule.AppliedToResources.Length != 0)
                JsonSerializer.Deserialize<List<string>>(string.Join(",", rule.AppliedToResources));

            if (rule.AppliedToResources != null || rule.AppliedToResources.Length != 0)
                JsonSerializer.Deserialize<List<string>>(string.Join(",", rule.AppliedToResources));

            if (rule.AppliedToUsers != null || rule.AppliedToUsers.Length != 0)
                JsonSerializer.Deserialize<List<string>>(string.Join(",", rule.AppliedToUsers));

            if (rule.AppliedToRegions != null || rule.AppliedToRegions.Length != 0)
                JsonSerializer.Deserialize<List<string>>(string.Join(",", rule.AppliedToRegions));

            if (rule.NotificationChannels != null || rule.NotificationChannels.Length != 0)
                JsonSerializer.Deserialize<List<string>>(string.Join(",", rule.NotificationChannels));

            // For a StreamJson instead of stringJson, we would use:
            // if (rule.NotificationChannels != null || rule.NotificationChannels.Length != 0)
            // if (!string.IsNullOrEmpty(rule.AppliedToResources))
            // {
            //     using var stream = new MemoryStream(Encoding.UTF8.GetBytes(string.Join(",", rule.AppliedToResources)));
            //     var appliedToProjects = JsonSerializer.Deserialize<List<string>>(stream);
            // }
        }

        private Dictionary<string, object> GetChangedProperties(ComplianceRule oldRule, ComplianceRule newRule)
        {
            var changes = new Dictionary<string, object>();
            var properties = typeof(ComplianceRule).GetProperties();

            foreach (var prop in properties)
            {
                var oldValue = prop.GetValue(oldRule);
                var newValue = prop.GetValue(newRule);

                if (!Equals(oldValue, newValue) && prop.Name != "UpdatedAt")
                {
                    changes[prop.Name] = new { old = oldValue, @new = newValue };
                }
            }

            return changes;
        }

        private async Task<DataHandlingResult> HandleAccessRequestAsync(DataRequest request)
        {
            // In a real implementation, this would fetch and return the user's data
            var userData = new
            {
                request.UserId,
                request.TenantId,
                RequestedAt = request.RequestedAt,
                DataTypes = new[] { "profile", "requests", "consents", "decisions" }
            };

            return new DataHandlingResult
            {
                RequestId = request.Id,
                Type = request.Type,
                Success = true,
                Data = JsonSerializer.Serialize(userData),
                RecordsAffected = 4,
                CompletedAt = DateTime.UtcNow
            };
        }

        private async Task<DataHandlingResult> HandlePortabilityRequestAsync(DataRequest request)
        {
            var exportResult = await ExportUserDataAsync(request.UserId);

            return new DataHandlingResult
            {
                RequestId = request.Id,
                Type = request.Type,
                Success = exportResult.Success,
                Data = exportResult.ExportFilePath,
                RecordsAffected = exportResult.TotalRecords,
                CompletedAt = DateTime.UtcNow
            };
        }

        private async Task<DataHandlingResult> HandleCorrectionRequestAsync(DataRequest request)
        {
            // In a real implementation, this would update user data
            // For now, we'll just acknowledge the request
            return new DataHandlingResult
            {
                RequestId = request.Id,
                Type = request.Type,
                Success = true,
                RecordsAffected = 0,
                CompletedAt = DateTime.UtcNow
            };
        }

        private async Task<DataHandlingResult> HandleDeletionRequestAsync(DataRequest request)
        {
            var deletionResult = await DeleteUserDataAsync(request.UserId);

            return new DataHandlingResult
            {
                RequestId = request.Id,
                Type = request.Type,
                Success = deletionResult.Success,
                RecordsAffected = deletionResult.RecordsDeleted,
                CompletedAt = DateTime.UtcNow
            };
        }

        private async Task CreateAuditTrailAsync(string? tenantId, string action, string resourceType,
            string resourceId, string? userId, Dictionary<string, object> details)
        {
            try
            {
                var auditTrail = new AuditTrail
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = tenantId ?? "system",
                    Action = action,
                    ResourceType = resourceType,
                    ResourceId = resourceId,
                    UserId = userId,
                    Details = details,
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditTrails.Add(auditTrail);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit trail");
                // Don't throw - audit trail failures shouldn't break the main flow
            }
        }

        private List<ComplianceCategoryScore> CalculateCategoryScores(List<ComplianceRule> rules, List<ComplianceLog> logs)
        {
            var categories = rules
                .GroupBy(r => r.RuleType)
                .Select(g => new
                {
                    Category = g.Key.ToString(),
                    Rules = g.ToList()
                })
                .ToList();

            var categoryScores = new List<ComplianceCategoryScore>();

            foreach (var category in categories)
            {
                var categoryLogs = logs
                    .Where(l => category.Rules.Any(r => r.Id == l.RuleId))
                    .ToList();

                var totalChecks = categoryLogs.Count;
                var passedChecks = categoryLogs.Count(l => l.Result.IsCompliant);

                var score = totalChecks > 0 ? (decimal)passedChecks / totalChecks * 100 : 100;

                categoryScores.Add(new ComplianceCategoryScore
                {
                    Category = category.Category,
                    Score = Math.Round(score, 2),
                    TotalRules = category.Rules.Count,
                    CompliantRules = category.Rules.Count(r => r.ComplianceRate >= 95),
                    NonCompliantRules = category.Rules.Count(r => r.ComplianceRate < 95),
                    AreasForImprovement = category.Rules
                        .Where(r => r.ComplianceRate < 95)
                        .Select(r => r.Name)
                        .ToList()
                });
            }

            return categoryScores;
        }

        private List<ComplianceImprovement> IdentifyImprovements(
            List<ComplianceCategoryScore> categoryScores,
            List<ComplianceViolation> violations)
        {
            var improvements = new List<ComplianceImprovement>();

            // Add improvements for low-scoring categories
            foreach (var category in categoryScores.Where(c => c.Score < 80))
            {
                improvements.Add(new ComplianceImprovement
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Improve {category.Category} Compliance",
                    Description = $"Current score: {category.Score}%. Focus on: {string.Join(", ", category.AreasForImprovement.Take(3))}",
                    Category = category.Category,
                    Priority = category.Score < 60 ? "High" : "Medium",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                });
            }

            // Add improvements for critical violations
            var criticalViolations = violations
                .Where(v => v.Severity == ViolationSeverity.Critical || v.Severity == ViolationSeverity.High)
                .GroupBy(v => v.RuleName)
                .ToList();

            foreach (var violationGroup in criticalViolations)
            {
                improvements.Add(new ComplianceImprovement
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = $"Resolve {violationGroup.Key} Violations",
                    Description = $"{violationGroup.Count()} critical/high severity violations detected",
                    Category = "Violations",
                    Priority = "Critical",
                    Status = "pending",
                    CreatedAt = DateTime.UtcNow
                });
            }

            return improvements;
        }

        #endregion
    }

    // Define IRuleEngine interface for rule evaluation
    public interface IRuleEngine
    {
        bool Evaluate(string condition, object context);
    }

    // Simple implementation of IRuleEngine
    public class JsonLogicRuleEngine : IRuleEngine
    {
        private readonly ILogger<JsonLogicRuleEngine> _logger;

        public JsonLogicRuleEngine(ILogger<JsonLogicRuleEngine> logger)
        {
            _logger = logger;
        }

        public bool Evaluate(string condition, object context)
        {
            try
            {
                // In a real implementation, this would use a proper rule engine like JsonLogic
                // For now, we'll implement a simple evaluator
                if (string.IsNullOrEmpty(condition) || condition.Trim() == "true")
                    return true;

                // Simple condition parsing
                if (condition.Contains("=="))
                {
                    var parts = condition.Split("==", StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 2)
                    {
                        var left = parts[0].Trim();
                        var right = parts[1].Trim().Trim('"', '\'');

                        // Try to get value from context
                        var contextValue = GetValueFromContext(context, left);
                        return contextValue?.ToString() == right;
                    }
                }

                // Default to false for safety
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error evaluating rule condition: {Condition}", condition);
                return false;
            }
        }

        private object? GetValueFromContext(object context, string path)
        {
            // Simple path resolution - in production, use a proper JSON path evaluator
            var properties = path.Split('.');
            object? current = context;

            foreach (var prop in properties)
            {
                if (current == null) return null;

                var property = current.GetType().GetProperty(prop);
                if (property == null) return null;

                current = property.GetValue(current);
            }

            return current;
        }
    }
}
