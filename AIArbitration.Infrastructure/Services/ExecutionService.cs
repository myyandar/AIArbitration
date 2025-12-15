using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    public class ExecutionService : IExecutionService
    {
        private readonly IArbitrationEngine _arbitrationEngine;
        private readonly IProviderAdapterFactory _adapterFactory;
        private readonly ICircuitBreaker _circuitBreaker;
        private readonly IComplianceService _complianceService;
        private readonly IModelRepository _modelRepository;
        private readonly IFallbackService _fallbackService;
        private readonly IRecordKeepingService _recordKeepingService;
        private readonly ILogger<ExecutionService> _logger;

        public ExecutionService(
            IArbitrationEngine arbitrationEngine,
            IProviderAdapterFactory adapterFactory,
            ICircuitBreaker circuitBreaker,
            IComplianceService complianceService,
            IModelRepository modelRepository,
            IFallbackService fallbackService,
            IRecordKeepingService recordKeepingService,
            ILogger<ExecutionService> logger)
        {
            _arbitrationEngine = arbitrationEngine ?? throw new ArgumentNullException(nameof(arbitrationEngine));
            _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _complianceService = complianceService ?? throw new ArgumentNullException(nameof(complianceService));
            _modelRepository = modelRepository ?? throw new ArgumentNullException(nameof(modelRepository));
            _fallbackService = fallbackService ?? throw new ArgumentNullException(nameof(fallbackService));
            _recordKeepingService = recordKeepingService ?? throw new ArgumentNullException(nameof(recordKeepingService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();

            _logger.LogInformation(
                "Starting execution {ExecutionId} for request {RequestId} in tenant {TenantId}",
                executionId, request.Id, context.TenantId);

            try
            {
                ValidateChatRequest(request);
                var enrichedContext = EnrichContextWithRequest(context, request);
                var arbitrationResult = await _arbitrationEngine.SelectModelAsync(enrichedContext);
                var selectedModel = arbitrationResult.SelectedModel;

                var complianceCheck = await _complianceService.CheckRequestComplianceAsync(request, enrichedContext);
                if (!complianceCheck.IsCompliant)
                {
                    throw new ComplianceException($"Request compliance check failed: {string.Join(", ", complianceCheck.Violations)}");
                }

                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

                var response = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    var enrichedRequest = EnrichChatRequest(request, selectedModel);
                    return await adapter.SendChatCompletionAsync(enrichedRequest);
                }, selectedModel.Model.Provider.Name);

                stopwatch.Stop();

                await _modelRepository.UpdateModelPerformanceAsync(
                    selectedModel.Model.Id,
                    response.ProcessingTime,
                    response.Success);

                await _recordKeepingService.RecordUsageAsync(context, selectedModel, response, request);
                await _recordKeepingService.CheckBudgetWarningsAsync(context, response.Cost);
                await _recordKeepingService.RecordExecutionSuccessAsync(executionId, context, selectedModel, response, stopwatch.Elapsed);

                _logger.LogInformation(
                    "Execution completed {ExecutionId}. Model: {ModelId}, Tokens: {Input}/{Output}, Cost: {Cost:C}, Time: {Time}ms",
                    executionId,
                    selectedModel.Model.ProviderModelId,
                    response.InputTokens,
                    response.OutputTokens,
                    response.Cost,
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Execution failed {ExecutionId}", executionId);

                await _recordKeepingService.RecordExecutionFailureAsync(executionId, context, request, ex, stopwatch.Elapsed);

                if (context.EnableFallback)
                {
                    return await _fallbackService.TryFallbackExecutionAsync(request, context, ex);
                }

                throw;
            }
        }

        public async Task<StreamingModelResponse> ExecuteStreamingAsync(ChatRequest request, ArbitrationContext context)
        {
            var stopwatch = Stopwatch.StartNew();
            var executionId = Guid.NewGuid().ToString();

            _logger.LogInformation(
                "Starting streaming execution {ExecutionId} for request {RequestId}",
                executionId, request.Id);

            try
            {
                ValidateChatRequest(request);
                var enrichedContext = EnrichContextWithRequest(context, request);
                var arbitrationResult = await _arbitrationEngine.SelectModelAsync(enrichedContext);
                var selectedModel = arbitrationResult.SelectedModel;

                var complianceCheck = await _complianceService.CheckRequestComplianceAsync(request, enrichedContext);
                if (!complianceCheck.IsCompliant)
                {
                    throw new ComplianceException($"Request compliance check failed for streaming");
                }

                var adapter = await _adapterFactory.GetAdapterForModelAsync(selectedModel.Model.ProviderModelId);

                // FIX: Call SendStreamingChatCompletionAsync directly, not via circuit breaker
                var enrichedRequest = EnrichChatRequest(request, selectedModel);
                var streamingResponse = await adapter.SendStreamingChatCompletionAsync(enrichedRequest);

                stopwatch.Stop();

                // Replace the initialization of enhancedResponse in ExecuteStreamingAsync with the required GetCompletionAsync property set
                var enhancedResponse = new StreamingModelResponse
                {
                    Stream = streamingResponse.Stream,
                    ModelId = selectedModel.Model.ProviderModelId,
                    Provider = selectedModel.Model.Provider.Name,
                    ProcessingTime = stopwatch.Elapsed,
                    RequestId = executionId,
                    IsSuccess = true,
                    // Provide a default implementation for GetCompletionAsync as required by the type signature
                    GetCompletionAsync = streamingResponse.GetCompletionAsync ?? (async () => await Task.FromResult<StreamingCompletion>(null)),
                    OnCompletion = async (inputTokens, outputTokens, cost) =>
                    {
                        await _recordKeepingService.HandleStreamingCompletionAsync(
                            context,
                            selectedModel,
                            (int)inputTokens,
                            (int)outputTokens,
                            (decimal)cost,
                            stopwatch.Elapsed);
                    }
                };

                _logger.LogInformation(
                    "Streaming execution started {ExecutionId}. Model: {ModelId}",
                    executionId, selectedModel.Model.ProviderModelId);

                return enhancedResponse;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Streaming execution failed {ExecutionId}", executionId);

                return new StreamingModelResponse
                {
                    Stream = (IAsyncEnumerable<StreamingChunk>)AsyncEnumerable.Empty<string>(),
                    Error = ex.Message,
                    IsSuccess = false,
                    ProcessingTime = stopwatch.Elapsed,
                    // Provide a default implementation for required property
                    GetCompletionAsync = async () => await Task.FromResult<StreamingCompletion>(null)
                };
            }
        }

        public async Task<BatchExecutionResult> ExecuteBatchAsync(List<ChatRequest> requests, ArbitrationContext context)
        {
            _logger.LogInformation("Starting batch execution of {RequestCount} requests", requests.Count);

            var stopwatch = Stopwatch.StartNew();
            var batchId = Guid.NewGuid().ToString();

            var batchResult = new BatchExecutionResult
            {
                BatchId = batchId,
                StartTime = DateTime.UtcNow
            };

            try
            {
                var successfulResponses = new List<ModelResponse>();
                var failedRequests = new List<FailedRequest>();
                var totalCost = 0m;
                var totalProcessingTime = TimeSpan.Zero;
                var modelUsage = new Dictionary<string, int>();

                // Process each request with controlled concurrency
                var semaphore = new SemaphoreSlim(10); // Limit to 10 concurrent executions
                var tasks = new List<Task<ModelResponse?>>();

                foreach (var request in requests)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        await semaphore.WaitAsync();
                        try
                        {
                            return await ExecuteAsync(request, context);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Request failed in batch execution");
                            return null;
                        }
                        finally
                        {
                            semaphore.Release();
                        }
                    }));
                }

                var responses = await Task.WhenAll(tasks);

                for (int i = 0; i < responses.Length; i++)
                {
                    var response = responses[i];
                    var request = requests[i];

                    if (response != null && response.Success)
                    {
                        successfulResponses.Add(response);
                        totalCost += response.Cost;
                        totalProcessingTime += response.ProcessingTime;

                        var modelId = response.ModelId;
                        modelUsage[modelId] = modelUsage.GetValueOrDefault(modelId) + 1;
                    }
                    else
                    {
                        failedRequests.Add(new FailedRequest
                        {
                            Request = request,
                            ErrorMessage = response?.ErrorMessage ?? "Execution failed",
                            ErrorCode = "BATCH_EXECUTION_ERROR",
                            ModelAttempted = response?.ModelId,
                            ProviderAttempted = response?.Provider,
                            FailedAt = DateTime.UtcNow,
                            Context = new Dictionary<string, object>
                            {
                                ["batch_id"] = batchId,
                                ["request_index"] = i
                            }
                        });
                    }
                }

                stopwatch.Stop();

                batchResult.EndTime = DateTime.UtcNow;
                batchResult.TotalProcessingTime = stopwatch.Elapsed;
                batchResult.SuccessfulResponses = successfulResponses;
                batchResult.FailedRequests = failedRequests;
                batchResult.TotalCost = totalCost;
                batchResult.TotalProcessingTime = totalProcessingTime;
                batchResult.ModelsUsed = modelUsage;

                _logger.LogInformation(
                    "Batch execution {BatchId} completed: {SuccessfulCount}/{TotalCount} successful, Total cost: {TotalCost:C}, Time: {TimeMs}ms",
                    batchId,
                    successfulResponses.Count,
                    requests.Count,
                    totalCost,
                    stopwatch.ElapsedMilliseconds);

                return batchResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "Batch execution {BatchId} failed", batchId);

                batchResult.EndTime = DateTime.UtcNow;
                batchResult.TotalProcessingTime = stopwatch.Elapsed;
                batchResult.Error = ex.Message.ToString();

                throw;
            }
        }

        private void ValidateChatRequest(ChatRequest request)
        {
            if (string.IsNullOrEmpty(request.Id))
                throw new ArgumentException("Request Id is required", nameof(request));

            if (request.Messages == null || !request.Messages.Any())
                throw new ArgumentException("At least one message is required", nameof(request));

            if (request.MaxTokens <= 0)
                throw new ArgumentException("MaxTokens must be greater than 0", nameof(request));
        }

        private ArbitrationContext EnrichContextWithRequest(ArbitrationContext context, ChatRequest request)
        {
            var enrichedContext = new ArbitrationContext
            {
                TenantId = context.TenantId,
                UserId = context.UserId,
                ProjectId = context.ProjectId,
                TaskType = context.TaskType,
                MaxCost = context.MaxCost,
                MinIntelligenceScore = context.MinIntelligenceScore,
                MaxLatency = context.MaxLatency,
                MinContextLength = context.MinContextLength,
                RequiredCapabilities = context.RequiredCapabilities,
                AllowedProviders = context.AllowedProviders,
                BlockedProviders = context.BlockedProviders,
                AllowedModels = context.AllowedModels,
                BlockedModels = context.BlockedModels,
                RequiredRegion = context.RequiredRegion,
                RequireDataResidency = context.RequireDataResidency,
                RequireEncryptionAtRest = context.RequireEncryptionAtRest,
                EnableFallback = context.EnableFallback,
                MaxFallbackAttempts = context.MaxFallbackAttempts,
                SelectionStrategy = context.SelectionStrategy,
                EstimatedInputTokens = EstimateTokensFromMessages(request.Messages),
                EstimatedOutputTokens = request.MaxTokens
            };

            if (string.IsNullOrEmpty(enrichedContext.TaskType))
            {
                enrichedContext.TaskType = DetermineTaskTypeFromRequest(request);
            }

            return enrichedContext;
        }

        private ChatRequest EnrichChatRequest(ChatRequest request, ArbitrationCandidate selectedModel)
        {
            request.ModelId = selectedModel.Model.ProviderModelId;
            request.Metadata ??= new Dictionary<string, string>
            {
                ["arbitration_model_id"] = selectedModel.Model.Id,
                ["arbitration_score"] = selectedModel.FinalScore.ToString("F2"),
                ["provider"] = selectedModel.Model.Provider.Name
            };
            return request;
        }

        private int EstimateTokensFromMessages(List<ChatMessage> messages)
        {
            if (messages == null || !messages.Any())
                return 0;

            return (int)Math.Ceiling(messages.Sum(m => m.Content?.Length ?? 0) / 4.0);
        }

        private string DetermineTaskTypeFromRequest(ChatRequest request)
        {
            var content = string.Join(" ", request.Messages.Select(m => m.Content ?? ""));

            if (content.Contains("summarize", StringComparison.OrdinalIgnoreCase))
                return "summarization";
            if (content.Contains("translate", StringComparison.OrdinalIgnoreCase))
                return "translation";
            if (content.Contains("code", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("program", StringComparison.OrdinalIgnoreCase))
                return "code_generation";
            if (content.Contains("analyze", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("explain", StringComparison.OrdinalIgnoreCase))
                return "analysis";

            return "chat";
        }
    }
}
