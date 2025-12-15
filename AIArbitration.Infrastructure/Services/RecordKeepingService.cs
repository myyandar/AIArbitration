using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services
{
    public class RecordKeepingService : IRecordKeepingService
    {
        private readonly IModelRepository _modelRepository;
        private readonly ICostTrackingService _costTracker;
        private readonly IBudgetService _budgetService;
        private readonly AIArbitrationDbContext _context;
        private readonly ILogger<RecordKeepingService> _logger;

        public RecordKeepingService(
            IModelRepository modelRepository,
            ICostTrackingService costTracker,
            IBudgetService budgetService,
            AIArbitrationDbContext context,
            ILogger<RecordKeepingService> logger)
        {
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _costTracker = costTracker ?? throw new ArgumentNullException(nameof(costTracker));
            _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task RecordArbitrationDecisionAsync(
            string decisionId,
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            List<ArbitrationCandidate> allCandidates,
            TimeSpan selectionDuration)
        {
            var decision = new ArbitrationDecision
            {
                Id = decisionId,
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                SelectedModelId = selectedModel.Model.Id,
                TaskType = context.TaskType,
                CandidateCount = allCandidates.Count,
                SelectionDuration = selectionDuration,
                Success = true,
                Timestamp = DateTime.UtcNow,
                DecisionFactorsJson = JsonSerializer.Serialize(new
                {
                    context.MinIntelligenceScore,
                    context.MaxCost,
                    context.MaxLatency,
                    context.RequiredRegion,
                    context.RequireDataResidency,
                    context.RequireEncryptionAtRest,
                    RequiredCapabilities = context.RequiredCapabilities?.ToList(),
                    SelectionStrategy = context.SelectionStrategy,
                    EstimatedCost = selectedModel.TotalCost,
                    FinalScore = selectedModel.FinalScore
                })
            };

            await _modelRepository.RecordArbitrationDecisionAsync(decision);
        }

        public async Task RecordArbitrationFailureAsync(
            string decisionId,
            ArbitrationContext context,
            Exception exception,
            TimeSpan duration)
        {
            var decision = new ArbitrationDecision
            {
                Id = decisionId,
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                TaskType = context.TaskType,
                CandidateCount = 0,
                SelectionDuration = duration,
                Success = false,
                ErrorMessage = exception.Message,
                ErrorType = exception.GetType().Name,
                Timestamp = DateTime.UtcNow
            };

            await _modelRepository.RecordArbitrationDecisionAsync(decision);
        }

        public async Task RecordUsageAsync(
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            ModelResponse response,
            ChatRequest request)
        {
            var usageRecord = new UsageRecord
            {
                Id = Guid.NewGuid().ToString(),
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                ModelId = selectedModel.Model.ProviderModelId,
                Provider = selectedModel.Model.Provider.Name,
                InputTokens = response.InputTokens,
                OutputTokens = response.OutputTokens,
                Cost = response.Cost,
                ProcessingTime = response.ProcessingTime,
                Timestamp = DateTime.UtcNow,
                RequestId = request.Id,
                Success = response.Success,
                Metadata = new Dictionary<string, string>
                {
                    ["decision_id"] = response.RequestId,
                    ["task_type"] = context.TaskType,
                    ["model_tier"] = selectedModel.Model.Tier.ToString()
                }
            };

            await _costTracker.RecordUsageAsync(usageRecord);
        }

        public async Task CheckBudgetWarningsAsync(ArbitrationContext context, decimal cost)
        {
            try
            {
                var budgetStatus = await _budgetService.GetBudgetStatusAsync(
                    context.TenantId, context.ProjectId, context.UserId);

                if (budgetStatus.UsagePercentage >= 90)
                {
                    _logger.LogWarning(
                        "Budget critically low for tenant {TenantId}: {Percentage:F1}% used",
                        context.TenantId, budgetStatus.UsagePercentage);
                }
                else if (budgetStatus.UsagePercentage >= 70)
                {
                    _logger.LogInformation(
                        "Budget warning for tenant {TenantId}: {Percentage:F1}% used",
                        context.TenantId, budgetStatus.UsagePercentage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check budget warnings for tenant {TenantId}", context.TenantId);
            }
        }

        public async Task HandleStreamingCompletionAsync(
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            int inputTokens,
            int outputTokens,
            decimal cost,
            TimeSpan processingTime)
        {
            try
            {
                // Update model performance
                await _modelRepository.UpdateModelPerformanceAsync(
                    selectedModel.Model.Id,
                    processingTime,
                    true);

                // Record usage
                var usageRecord = new UsageRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = context.TenantId,
                    UserId = context.UserId,
                    ProjectId = context.ProjectId,
                    ModelId = selectedModel.Model.ProviderModelId,
                    Provider = selectedModel.Model.Provider.Name,
                    InputTokens = inputTokens,
                    OutputTokens = outputTokens,
                    Cost = cost,
                    ProcessingTime = processingTime,
                    Timestamp = DateTime.UtcNow,
                    RequestId = Guid.NewGuid().ToString(),
                    Success = true,
                    RecordType = "streaming"
                };

                await _costTracker.RecordUsageAsync(usageRecord);

                // Check budget
                await CheckBudgetWarningsAsync(context, cost);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling streaming completion");
            }
        }

        public async Task RecordExecutionSuccessAsync(
            string executionId,
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            ModelResponse response,
            TimeSpan duration)
        {
            var executionLog = new ExecutionLog
            {
                Id = executionId,
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                ModelId = selectedModel.Model.Id,
                Provider = selectedModel.Model.Provider.Name,
                TaskType = context.TaskType,
                InputTokens = response.InputTokens,
                OutputTokens = response.OutputTokens,
                Cost = response.Cost,
                Duration = duration,
                Success = true,
                Timestamp = DateTime.UtcNow
            };

            await _context.ExecutionLogs.AddAsync(executionLog);
            await _context.SaveChangesAsync();
        }

        public async Task RecordExecutionFailureAsync(
            string executionId,
            ArbitrationContext context,
            ChatRequest request,
            Exception exception,
            TimeSpan duration)
        {
            var executionLog = new ExecutionLog
            {
                Id = executionId,
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                TaskType = context.TaskType,
                Duration = duration,
                Success = false,
                ErrorMessage = exception.Message,
                ErrorType = exception.GetType().Name,
                Timestamp = DateTime.UtcNow,
                RequestMetadata = JsonSerializer.Serialize(new
                {
                    request.Messages?.Count,
                    request.MaxTokens,
                    request.Temperature
                })
            };

            await _context.ExecutionLogs.AddAsync(executionLog);
            await _context.SaveChangesAsync();
        }
    }
}
