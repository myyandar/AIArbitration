using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Providers
{
    #region Base Provider Adapter

    /// <summary>
    /// Base implementation for all provider adapters with comprehensive database integration
    /// </summary>
    public abstract class BaseProviderAdapter : IProviderAdapter
    {
        protected readonly AIArbitrationDbContext _dbContext;
        protected readonly IHttpClientFactory _httpClientFactory;
        protected readonly ILogger<BaseProviderAdapter> _logger;
        protected readonly ICircuitBreaker _circuitBreaker;
        protected readonly IRateLimiter _rateLimiter;
        protected readonly ModelProvider _provider;
        protected readonly ProviderConfiguration _configuration;
        protected readonly JsonSerializerOptions _jsonOptions;
        protected readonly IMemoryCache _cache;

        public string ProviderName => _provider.Name;
        public string ProviderId => _provider.Id;
        public string BaseUrl => _provider.BaseUrl;

        protected BaseProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<BaseProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider provider,
            ProviderConfiguration configuration)
        {
            _dbContext = dbContext;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _circuitBreaker = circuitBreaker;
            _rateLimiter = rateLimiter;
            _cache = cache;
            _provider = provider;
            _configuration = configuration;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        #region Abstract Methods for Provider-Specific Implementations

        protected abstract Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request);
        protected abstract Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request);
        protected abstract Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request);
        protected abstract Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request);

        protected abstract Task<ModelResponse> ParseChatCompletionResponseAsync(
            HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime);
        protected abstract Task<EmbeddingResponse> ParseEmbeddingResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime);
        protected abstract Task<ModerationResponse> ParseModerationResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime);

        protected abstract Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response);
        protected abstract Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response);

        protected abstract ProviderHealthStatus ParseHealthCheckResponse(HttpResponseMessage response);
        protected abstract Task<HttpRequestMessage> CreateHealthCheckRequestAsync();

        #endregion

        #region Core Operations Implementation

        public virtual async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            ValidateRequest(request);
            await CheckCircuitBreakerAsync();
            await CheckRateLimiterAsync();

            var startTime = DateTime.UtcNow;
            HttpResponseMessage response = null;
            string requestId = request.Id ?? Guid.NewGuid().ToString();

            try
            {
                // Create audit log
                await CreateErrorLogAsync(requestId, "ChatCompletion", request.ModelId, "STARTED");

                // Create HTTP request
                var httpRequest = await CreateChatCompletionRequestAsync(request);
                AddDefaultHeaders(httpRequest);
                AddRequestHeaders(httpRequest, request);

                // Send request with retry logic
                response = await SendWithRetryAsync(httpRequest, requestId);

                // Parse response
                var processingTime = DateTime.UtcNow - startTime;
                var modelResponse = await ParseChatCompletionResponseAsync(response, request, processingTime);
                modelResponse.RequestId = requestId;

                // Record success
                await RecordSuccessAsync();
                await UpdatePerformanceMetricsAsync(processingTime, true, request.ModelId);
                await RecordUsageAsync(modelResponse);
                await UpdateModelUsageAsync(request.ModelId, modelResponse.InputTokens, modelResponse.OutputTokens);

                // Create successful audit log
                await CreateErrorLogAsync(requestId, "ChatCompletion", request.ModelId, "COMPLETED",
                    processingTime.TotalMilliseconds, true);

                return modelResponse;
            }
            catch (Exception ex)
            {
                await HandleRequestErrorAsync(ex, requestId, "ChatCompletion", request.ModelId, startTime);
                throw;
            }
            finally
            {
                response?.Dispose();
            }
        }

        public virtual async Task<StreamingModelResponse> SendStreamingChatCompletionAsync(ChatRequest request)
        {
            ValidateRequest(request);
            await CheckCircuitBreakerAsync();
            await CheckRateLimiterAsync();

            var startTime = DateTime.UtcNow;
            HttpResponseMessage response = null;
            string requestId = request.Id ?? Guid.NewGuid().ToString();
            string streamId = Guid.NewGuid().ToString();

            try
            {
                // Create audit log
                await CreateErrorLogAsync(requestId, "StreamingChatCompletion", request.ModelId, "STARTED");

                // Create HTTP request
                var httpRequest = await CreateStreamingChatCompletionRequestAsync(request);
                AddDefaultHeaders(httpRequest);
                AddRequestHeaders(httpRequest, request);

                // Send request with retry logic
                response = await SendWithRetryAsync(httpRequest, requestId);

                // Create streaming response
                var stream = await CreateStreamingResponseAsync(response, streamId, requestId, request.ModelId);

                var streamingResponse = new StreamingModelResponse
                {
                    Id = streamId,
                    ModelUsed = request.ModelId,
                    Provider = ProviderName,
                    Stream = stream,
                    RequestId = requestId,
                    SessionId = request.SessionId,
                    StartTime = startTime,
                    GetCompletionAsync = async () => await GetStreamingCompletionAsync(streamId, requestId, request)
                };

                // Record initial success
                await RecordSuccessAsync();
                await CreateErrorLogAsync(requestId, "StreamingChatCompletion", request.ModelId, "STREAMING_STARTED");

                return streamingResponse;
            }
            catch (Exception ex)
            {
                await HandleRequestErrorAsync(ex, requestId, "StreamingChatCompletion", request.ModelId, startTime);
                throw;
            }
            finally
            {
                response?.Dispose();
            }
        }

        public virtual async Task<EmbeddingResponse> SendEmbeddingAsync(EmbeddingRequest request)
        {
            ValidateEmbeddingRequest(request);
            await CheckCircuitBreakerAsync();
            await CheckRateLimiterAsync();

            var startTime = DateTime.UtcNow;
            HttpResponseMessage response = null;
            string requestId = request.Id ?? Guid.NewGuid().ToString();

            try
            {
                // Create audit log
                await CreateErrorLogAsync(requestId, "Embedding", request.ModelId, "STARTED");

                // Create HTTP request
                var httpRequest = await CreateEmbeddingRequestAsync(request);
                AddDefaultHeaders(httpRequest);

                // Send request with retry logic
                response = await SendWithRetryAsync(httpRequest, requestId);

                // Parse response
                var processingTime = DateTime.UtcNow - startTime;
                var embeddingResponse = await ParseEmbeddingResponseAsync(response, processingTime);
                embeddingResponse.RequestId = requestId;

                // Record success
                await RecordSuccessAsync();
                await UpdatePerformanceMetricsAsync(processingTime, true, request.ModelId);
                await UpdateModelUsageAsync(request.ModelId, embeddingResponse.InputTokens, 0);

                // Create successful audit log
                await CreateErrorLogAsync(requestId, "Embedding", request.ModelId, "COMPLETED",
                    processingTime.TotalMilliseconds, true);

                return embeddingResponse;
            }
            catch (Exception ex)
            {
                await HandleRequestErrorAsync(ex, requestId, "Embedding", request.ModelId, startTime);
                throw;
            }
            finally
            {
                response?.Dispose();
            }
        }

        public virtual async Task<ModerationResponse> SendModerationAsync(ModerationRequest request)
        {
            ValidateModerationRequest(request);
            await CheckCircuitBreakerAsync();
            await CheckRateLimiterAsync();

            var startTime = DateTime.UtcNow;
            HttpResponseMessage response = null;
            string requestId = request.Id ?? Guid.NewGuid().ToString();

            try
            {
                // Create error log
                await CreateErrorLogAsync(requestId, "Moderation", request.ModelId, "STARTED");

                // Create HTTP request
                var httpRequest = await CreateModerationRequestAsync(request);
                AddDefaultHeaders(httpRequest);

                // Send request with retry logic
                response = await SendWithRetryAsync(httpRequest, requestId);

                // Parse response
                var processingTime = DateTime.UtcNow - startTime;
                var moderationResponse = await ParseModerationResponseAsync(response, processingTime);
                moderationResponse.Id = requestId;

                // Record success
                await RecordSuccessAsync();
                await UpdatePerformanceMetricsAsync(processingTime, true, request.ModelId);

                // Create successful audit log
                await CreateErrorLogAsync(requestId, "Moderation", request.ModelId, "COMPLETED",
                    processingTime.TotalMilliseconds, true);

                return moderationResponse;
            }
            catch (Exception ex)
            {
                await HandleRequestErrorAsync(ex, requestId, "Moderation", request.ModelId, startTime);
                throw;
            }
            finally
            {
                response?.Dispose();
            }
        }

        #endregion

        #region Model Management Implementation

        public virtual async Task<List<ProviderModelInfo>> GetAvailableModelsAsync()
        {
            await CheckCircuitBreakerAsync();

            var cacheKey = $"{ProviderId}_models";
            if (_cache.TryGetValue(cacheKey, out List<ProviderModelInfo> cachedModels))
            {
                return cachedModels;
            }

            HttpResponseMessage response = null;

            try
            {
                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models");
                AddDefaultHeaders(request);

                // Send request
                var httpClient = _httpClientFactory.CreateClient(ProviderId);
                response = await httpClient.SendAsync(request);

                // Check response
                response.EnsureSuccessStatusCode();

                // Parse response
                var models = await ParseAvailableModelsResponseAsync(response);

                // Update database with model information
                await UpdateModelsInDatabaseAsync(models);

                // Cache the results
                _cache.Set(cacheKey, models, TimeSpan.FromMinutes(30));

                return models;
            }
            catch (HttpRequestException ex)
            {
                await RecordFailureAsync(ex);
                throw new ProviderException($"Failed to get available models: {ex.Message}", ProviderId, ex);
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);
                throw new ProviderException($"Failed to get available models: {ex.Message}", ProviderId, ex);
            }
            finally
            {
                response?.Dispose();
            }
        }

        public virtual async Task<ProviderModelInfo?> GetModelInfoAsync(string modelId)
        {
            if (string.IsNullOrEmpty(modelId))
                throw new ArgumentException("Model ID cannot be null or empty", nameof(modelId));

            await CheckCircuitBreakerAsync();

            var cacheKey = $"{ProviderId}_model_{modelId}";
            if (_cache.TryGetValue(cacheKey, out ProviderModelInfo cachedModel))
            {
                return cachedModel;
            }

            HttpResponseMessage response = null;

            try
            {
                // Create HTTP request
                var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/models/{modelId}");
                AddDefaultHeaders(request);

                // Send request
                var httpClient = _httpClientFactory.CreateClient(ProviderId);
                response = await httpClient.SendAsync(request);

                // Check response
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;

                response.EnsureSuccessStatusCode();

                // Parse response
                var modelInfo = await ParseModelInfoResponseAsync(response);

                // Cache the result
                _cache.Set(cacheKey, modelInfo, TimeSpan.FromMinutes(15));

                return modelInfo;
            }
            catch (HttpRequestException ex)
            {
                await RecordFailureAsync(ex);
                throw new ProviderException($"Failed to get model info: {ex.Message}", ProviderId, ex);
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);
                throw new ProviderException($"Failed to get model info: {ex.Message}", ProviderId, ex);
            }
            finally
            {
                response?.Dispose();
            }
        }

        #endregion

        #region Health and Monitoring Implementation

        public virtual async Task<ProviderHealthStatus> CheckHealthAsync()
        {
            var cacheKey = $"{ProviderId}_health";
            if (_cache.TryGetValue(cacheKey, out ProviderHealthStatus cachedStatus))
            {
                return cachedStatus;
            }

            try
            {
                // Check circuit breaker first
                var circuitId = $"Provider_{ProviderId}";
                var circuitState = await _circuitBreaker.GetCircuitStateAsync(circuitId);

                if (circuitState.Status != CircuitStatus.Open)
                {
                    _cache.Set(cacheKey, ProviderHealthStatus.Unknown, TimeSpan.FromSeconds(30));
                    return ProviderHealthStatus.Unknown;
                }

                // Make health check request
                var request = await CreateHealthCheckRequestAsync();
                AddDefaultHeaders(request);

                var httpClient = _httpClientFactory.CreateClient(ProviderId);
                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

                var healthStatus = ParseHealthCheckResponse(response);

                // Update provider health in database
                await UpdateProviderHealthAsync(healthStatus);

                // Cache the result
                _cache.Set(cacheKey, healthStatus, TimeSpan.FromSeconds(60));

                return healthStatus;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health check failed for provider: {ProviderId}", ProviderId);
                await RecordFailureAsync(ex);

                var fallbackStatus = ProviderHealthStatus.Unstable;
                _cache.Set(cacheKey, fallbackStatus, TimeSpan.FromSeconds(30));

                return fallbackStatus;
            }
        }
        #endregion

        #region Cost Management Implementation

        public virtual async Task<CostEstimation> EstimateCostAsync(ChatRequest request)
        {
            ValidateRequest(request);

            try
            {
                // Get model pricing from cache or database
                var pricing = await GetModelPricingAsync(request.ModelId);

                // Estimate token counts
                var inputTokens = EstimateInputTokens(request);
                var outputTokens = EstimateOutputTokens(request);
                var totalTokens = inputTokens + outputTokens;

                // Calculate costs
                var costs = CalculateCosts(pricing, inputTokens, outputTokens);

                // Get historical accuracy for confidence calculation
                var accuracy = await GetEstimationAccuracyAsync(request.ModelId);

                return new CostEstimation
                {
                    EstimatedCost = Math.Round(costs.TotalCost, 6),
                    InputCost = Math.Round(costs.InputCost, 6),
                    OutputCost = Math.Round(costs.OutputCost, 6),
                    ServiceCost = Math.Round(costs.ServiceCost, 6),
                    EstimatedInputTokens = inputTokens,
                    EstimatedOutputTokens = outputTokens,
                    TotalTokens = totalTokens,
                    Currency = "USD",
                    Confidence = accuracy,
                    PricingModel = pricing.PricingModel,
                    PricePerInputToken = pricing.InputTokenPrice,
                    PricePerOutputToken = pricing.OutputTokenPrice,
                    ServiceFeePercentage = _configuration.ServiceFeePercentage,
                    ServiceFee = costs.ServiceCost > 0 ? Math.Round(costs.ServiceCost, 6) : (decimal?)null,
                    CostBreakdown = new Dictionary<string, decimal>
                    {
                        { "Input Tokens", Math.Round(costs.InputCost, 6) },
                        { "Output Tokens", Math.Round(costs.OutputCost, 6) },
                        { "Service Fee", Math.Round(costs.ServiceCost, 6) }
                    },
                    ModelId = request.ModelId,
                    ProviderId = ProviderId,
                    Timestamp = DateTime.UtcNow,
                    IsDetailedEstimation = true,
                    Notes = $"Based on {pricing.PricingModel} pricing model"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating cost for request");
                return CreateFallbackCostEstimation(request);
            }
        }

        public virtual async Task<CostEstimation> EstimateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            if (inputTokens < 0)
                throw new ArgumentException("Input tokens cannot be negative", nameof(inputTokens));

            if (outputTokens < 0)
                throw new ArgumentException("Output tokens cannot be negative", nameof(outputTokens));

            try
            {
                // Get model pricing
                var pricing = await GetModelPricingAsync(modelId);
                var totalTokens = inputTokens + outputTokens;

                // Calculate costs
                var costs = CalculateCosts(pricing, inputTokens, outputTokens);

                return new CostEstimation
                {
                    EstimatedCost = Math.Round(costs.TotalCost, 6),
                    InputCost = Math.Round(costs.InputCost, 6),
                    OutputCost = Math.Round(costs.OutputCost, 6),
                    ServiceCost = Math.Round(costs.ServiceCost, 6),
                    EstimatedInputTokens = inputTokens,
                    EstimatedOutputTokens = outputTokens,
                    TotalTokens = totalTokens,
                    Currency = "USD",
                    Confidence = 0.8m,
                    PricingModel = pricing.PricingModel,
                    PricePerInputToken = pricing.InputTokenPrice,
                    PricePerOutputToken = pricing.OutputTokenPrice,
                    ServiceFeePercentage = _configuration.ServiceFeePercentage,
                    ServiceFee = costs.ServiceCost > 0 ? Math.Round(costs.ServiceCost, 6) : (decimal?)null,
                    CostBreakdown = new Dictionary<string, decimal>
                    {
                        { "Input Tokens", Math.Round(costs.InputCost, 6) },
                        { "Output Tokens", Math.Round(costs.OutputCost, 6) },
                        { "Service Fee", Math.Round(costs.ServiceCost, 6) }
                    },
                    ModelId = modelId,
                    ProviderId = ProviderId,
                    Timestamp = DateTime.UtcNow,
                    IsDetailedEstimation = true
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error estimating cost for model: {ModelId}", modelId);
                return CreateFallbackCostEstimation(inputTokens, outputTokens);
            }
        }

        #endregion

        #region Configuration Management

        public virtual ProviderConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public virtual async Task UpdateConfigurationAsync(ProviderConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                // Update in-memory configuration
                _configuration.ApiKey = configuration.ApiKey;
                _configuration.ApiSecret = configuration.ApiSecret;
                _configuration.Timeout = configuration.Timeout;
                _configuration.MaxRetries = configuration.MaxRetries;
                _configuration.RetryDelay = configuration.RetryDelay;
                _configuration.ServiceFeePercentage = configuration.ServiceFeePercentage;
                _configuration.EnableCircuitBreaker = configuration.EnableCircuitBreaker;
                _configuration.EnableRateLimiting = configuration.EnableRateLimiting;
                _configuration.RequestsPerMinute = configuration.RequestsPerMinute;
                _configuration.CustomHeaders = configuration.CustomHeaders;
                _configuration.UpdatedAt = DateTime.UtcNow;

                // Update in database
                var dbConfig = await _dbContext.ProviderConfigurations
                    .FirstOrDefaultAsync(c => c.ProviderId == ProviderId);

                if (dbConfig != null)
                {
                    dbConfig.ApiKey = configuration.ApiKey;
                    dbConfig.ApiSecret = configuration.ApiSecret;
                    dbConfig.Timeout = configuration.Timeout;
                    dbConfig.MaxRetries = configuration.MaxRetries;
                    dbConfig.RetryDelay = configuration.RetryDelay;
                    dbConfig.ServiceFeePercentage = configuration.ServiceFeePercentage;
                    dbConfig.EnableCircuitBreaker = configuration.EnableCircuitBreaker;
                    dbConfig.EnableRateLimiting = configuration.EnableRateLimiting;
                    dbConfig.RequestsPerMinute = configuration.RequestsPerMinute;
                    dbConfig.CustomHeaders = configuration.CustomHeaders;
                    dbConfig.UpdatedAt = DateTime.UtcNow;

                    _dbContext.ProviderConfigurations.Update(dbConfig);
                }
                else
                {
                    configuration.Id = Guid.NewGuid().ToString();
                    configuration.ProviderId = ProviderId;
                    configuration.CreatedAt = DateTime.UtcNow;
                    configuration.UpdatedAt = DateTime.UtcNow;

                    await _dbContext.ProviderConfigurations.AddAsync(configuration);
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                // Clear related caches
                _cache.Remove($"{ProviderId}_configuration");
                _cache.Remove($"{ProviderId}_health");

                _logger.LogInformation("Configuration updated for provider: {ProviderId}", ProviderId);

                // Log configuration change
                await CreateConfigurationChangeLogAsync(configuration);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating configuration for provider: {ProviderId}", ProviderId);
                throw new ProviderException($"Failed to update configuration: {ex.Message}", ProviderId, ex);
            }
        }

        #endregion

        #region Protected Helper Methods

        protected virtual void AddDefaultHeaders(HttpRequestMessage request)
        {
            if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
            }

            request.Headers.Add("User-Agent", "AIArbitration/1.0");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // Add provider-specific headers
            AddProviderSpecificHeaders(request);

            if (!string.IsNullOrEmpty(_configuration.CustomHeaders))
            {
                try
                {
                    var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(_configuration.CustomHeaders);
                    foreach (var header in headers)
                    {
                        request.Headers.Add(header.Key, header.Value);
                    }
                }
                catch (JsonException)
                {
                    // Ignore malformed custom headers
                }
            }
        }

        protected virtual void AddProviderSpecificHeaders(HttpRequestMessage request)
        {
            // Base implementation adds no provider-specific headers
            // Override in derived classes for provider-specific headers
        }

        protected virtual void AddRequestHeaders(HttpRequestMessage request, ChatRequest chatRequest)
        {
            // Add request-specific headers
            if (!string.IsNullOrEmpty(chatRequest.RequestId))
            {
                request.Headers.Add("X-Request-ID", chatRequest.RequestId);
            }

            if (!string.IsNullOrEmpty(chatRequest.SessionId))
            {
                request.Headers.Add("X-Session-ID", chatRequest.SessionId);
            }

            if (!string.IsNullOrEmpty(chatRequest.TenantId))
            {
                request.Headers.Add("X-Tenant-ID", chatRequest.TenantId);
            }

            if (!string.IsNullOrEmpty(chatRequest.UserId))
            {
                request.Headers.Add("X-User-ID", chatRequest.UserId);
            }
        }

        protected virtual async Task<HttpResponseMessage> SendWithRetryAsync(
            HttpRequestMessage request, string requestId, int maxRetries = 3)
        {
            Exception lastException = null;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var httpClient = _httpClientFactory.CreateClient(ProviderId);
                    httpClient.Timeout = _configuration.Timeout;

                    var response = await httpClient.SendAsync(request);

                    if (response.IsSuccessStatusCode || !ShouldRetry(response.StatusCode))
                    {
                        return response;
                    }

                    // Log retry
                    _logger.LogWarning("Request {RequestId} attempt {Attempt} failed with status {StatusCode}, retrying...",
                        requestId, attempt, response.StatusCode);

                    response.Dispose();
                }
                catch (Exception ex) when (IsTransientException(ex))
                {
                    lastException = ex;
                    _logger.LogWarning(ex, "Request {RequestId} attempt {Attempt} failed with transient error, retrying...",
                        requestId, attempt);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }

                if (attempt < maxRetries)
                {
                    await Task.Delay(_configuration.RetryDelay * attempt);
                }
            }

            throw new ProviderException($"Request failed after {maxRetries} attempts", ProviderId, lastException);
        }

        protected virtual bool ShouldRetry(System.Net.HttpStatusCode statusCode)
        {
            return statusCode switch
            {
                System.Net.HttpStatusCode.RequestTimeout => true,
                System.Net.HttpStatusCode.TooManyRequests => true,
                System.Net.HttpStatusCode.InternalServerError => true,
                System.Net.HttpStatusCode.BadGateway => true,
                System.Net.HttpStatusCode.ServiceUnavailable => true,
                System.Net.HttpStatusCode.GatewayTimeout => true,
                _ => false
            };
        }

        protected virtual bool IsTransientException(Exception ex)
        {
            return ex is HttpRequestException ||
                   ex is TaskCanceledException ||
                   ex is TimeoutException ||
                   (ex.InnerException is System.Net.Sockets.SocketException socketEx &&
                    socketEx.SocketErrorCode == System.Net.Sockets.SocketError.ConnectionRefused);
        }

        protected virtual int EstimateInputTokens(ChatRequest request)
        {
            if (request.Messages == null || !request.Messages.Any())
                return 0;

            int totalTokens = 0;

            foreach (var message in request.Messages)
            {
                // Base tokens for message structure
                totalTokens += 4;

                // Content tokens (rough estimate: 4 chars per token for English)
                if (!string.IsNullOrEmpty(message.Content))
                {
                    totalTokens += (int)Math.Ceiling(message.Content.Length / 4.0);
                }

                // Tool/function call tokens
                if (message.ToolCalls != null)
                {
                    foreach (var toolCall in message.ToolCalls)
                    {
                        totalTokens += 10; // Base for tool call
                        if (!string.IsNullOrEmpty(toolCall.Function?.Name))
                            totalTokens += (int)Math.Ceiling(toolCall.Function.Name.Length / 4.0);
                        if (!string.IsNullOrEmpty(toolCall.Function?.Arguments))
                            totalTokens += (int)Math.Ceiling(toolCall.Function.Arguments.Length / 4.0);
                    }
                }

                // Function call tokens
                if (message.FunctionCall != null)
                {
                    totalTokens += 8; // Base for function call
                    if (!string.IsNullOrEmpty(message.FunctionCall.Name))
                        totalTokens += (int)Math.Ceiling(message.FunctionCall.Name.Length / 4.0);
                    if (!string.IsNullOrEmpty(message.FunctionCall.Arguments))
                        totalTokens += (int)Math.Ceiling(message.FunctionCall.Arguments.Length / 4.0);
                }
            }

            // System prompt tokens
            if (!string.IsNullOrEmpty(request.SystemPrompt))
            {
                totalTokens += (int)Math.Ceiling(request.SystemPrompt.Length / 4.0);
            }

            // Tools tokens
            if (request.Tools != null)
            {
                foreach (var tool in request.Tools)
                {
                    totalTokens += 15; // Base for tool definition
                    // Additional tokens for tool details would be added here
                }
            }

            return totalTokens;
        }

        protected virtual int EstimateOutputTokens(ChatRequest request)
        {
            return request.MaxTokens ?? _configuration.DefaultMaxTokens;
        }

        protected virtual async Task<IAsyncEnumerable<StreamingChunk>> CreateStreamingResponseAsync(
            HttpResponseMessage response, string streamId, string requestId, string modelId)
        {
            var stream = response.Content.ReadAsStreamAsync();
            return ParseStreamAsync(stream, streamId, requestId, modelId);
        }

        protected virtual async IAsyncEnumerable<StreamingChunk> ParseStreamAsync(
            Task<System.IO.Stream> streamTask, string streamId, string requestId, string modelId)
        {
            await using var stream = await streamTask;
            using var reader = new System.IO.StreamReader(stream);

            string line;
            int chunkIndex = 0;
            var startTime = DateTime.UtcNow;

            while ((line = await reader.ReadLineAsync()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var chunk = ParseStreamLine(line, chunkIndex, startTime);
                if (chunk != null)
                {
                    chunk.Id = $"{streamId}_{chunkIndex}";
                    chunk.StreamId = streamId;
                    chunk.RequestId = requestId;
                    chunk.Model = modelId;
                    chunk.Provider = ProviderName;

                    yield return chunk;

                    if (chunk.IsLastChunk)
                        yield break;

                    chunkIndex++;
                }
            }
        }

        protected virtual StreamingChunk ParseStreamLine(string line, int chunkIndex, DateTime startTime)
        {
            // Base implementation - override in derived classes for provider-specific parsing
            return new StreamingChunk
            {
                Id = Guid.NewGuid().ToString(),
                Content = line,
                IsFirstChunk = chunkIndex == 0,
                IsLastChunk = false,
                Timestamp = DateTime.UtcNow,
                ProcessingTime = DateTime.UtcNow - startTime
            };
        }

        protected virtual async Task<StreamingCompletion> GetStreamingCompletionAsync(
            string streamId, string requestId, ChatRequest originalRequest)
        {
            try
            {
                // Get streaming completion from database or cache
                // This would typically aggregate all chunks and calculate final metrics

                var completion = new StreamingCompletion
                {
                    ResponseId = Guid.NewGuid().ToString(),
                    StreamId = streamId,
                    ModelUsed = originalRequest.ModelId,
                    Provider = ProviderName,
                    Content = "[Streaming completed - aggregate content would be stored here]",
                    InputTokens = EstimateInputTokens(originalRequest),
                    OutputTokens = originalRequest.MaxTokens ?? _configuration.DefaultMaxTokens,
                    Cost = await CalculateActualCostAsync(streamId),
                    FinishReason = FinishReason.Stop,
                    TotalProcessingTime = TimeSpan.FromSeconds(2),
                    CompletedAt = DateTime.UtcNow,
                    RequestId = requestId,
                    SessionId = originalRequest.SessionId
                };

                // Record usage
                await RecordStreamingUsageAsync(completion);

                return completion;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting streaming completion for stream: {StreamId}", streamId);
                throw;
            }
        }

        protected virtual async Task CheckCircuitBreakerAsync()
        {
            if (_configuration.EnableCircuitBreaker)
            {
                var circuitId = $"Provider_{ProviderId}";
                var isAllowed = await _circuitBreaker.AllowRequestAsync(circuitId);

                if (!isAllowed)
                {
                    throw new CircuitBreakerOpenException($"Circuit breaker is open for provider: {ProviderId}", ProviderId, TimeSpan.FromSeconds(30));
                }
            }
        }

        protected virtual async Task CheckRateLimiterAsync()
        {
            if (_configuration.EnableRateLimiting)
            {
                var rateLimitKey = $"Provider_{ProviderId}";
                var isAllowed = await _rateLimiter.AllowRequestAsync(rateLimitKey, 1);

                if (!isAllowed)
                {
                    var resetTime = await _rateLimiter.GetResetTimeAsync(rateLimitKey);
                    throw new RateLimitExceededException(
                        $"Rate limit exceeded for provider: {ProviderId}", ProviderId, resetTime);
                }
            }
        }

        protected virtual async Task RecordSuccessAsync()
        {
            var circuitId = $"Provider_{ProviderId}";
            await _circuitBreaker.RecordSuccessAsync(circuitId);

            // Update provider health
            await UpdateProviderHealthAsync(ProviderHealthStatus.Healthy);
        }

        protected virtual async Task RecordFailureAsync(Exception exception)
        {
            var circuitId = $"Provider_{ProviderId}";
            await _circuitBreaker.RecordFailureAsync(circuitId, exception);

            // Update provider health
            await UpdateProviderHealthAsync(ProviderHealthStatus.Unstable);
        }

        protected virtual async Task UpdatePerformanceMetricsAsync(
            TimeSpan latency, bool success, string modelId)
        {
            try
            {
                var model = await _dbContext.AIModels.Where(p => p.Id == modelId).FirstOrDefaultAsync();
                var provider = model.Provider ?? null;

                var metric = new PerformanceAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    Provider = provider,
                    ModelId = modelId,
                    Latency = latency,
                    Success = success,
                    AnalysisPeriodEnd = DateTime.UtcNow,
                    AnalysisPeriodStart = DateTime.UtcNow,
                    //InputTokens = 0, // Would be populated from actual usage
                    //OutputTokens = 0,
                    //Cost = 0m
                };

                await _dbContext.PerformanceAnalysis.AddAsync(metric);

                // Also update provider's overall performance
                await UpdateProviderPerformanceAsync(latency, success);

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating performance metrics for provider: {ProviderId}", ProviderId);
            }
        }

        protected virtual async Task UpdateProviderPerformanceAsync(TimeSpan latency, bool success)
        {
            var performance = await _dbContext.PerformanceAnalysis
                .FirstOrDefaultAsync(p => p.ProviderId == ProviderId && p.AnalysisPeriodEnd == DateTime.UtcNow.Date);

            if (performance == null)
            {
                performance = new PerformanceAnalysis
                {
                    Id = Guid.NewGuid().ToString(),
                    ProviderId = ProviderId,
                    AnalysisPeriodEnd = DateTime.UtcNow.Date,
                    TotalRequests = 0,
                    SuccessfulRequests = 0,
                    FailedRequests = 0,
                    TotalLatency = TimeSpan.Zero,
                    MinLatency = TimeSpan.MaxValue,
                    MaxLatency = TimeSpan.Zero,
                    AnalysisPeriodStart = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                await _dbContext.PerformanceAnalysis.AddAsync(performance);
            }

            performance.TotalRequests++;
            if (success)
                performance.SuccessfulRequests++;
            else
                performance.FailedRequests++;

            performance.TotalLatency += latency;
            if (latency < performance.MinLatency)
                performance.MinLatency = latency;
            if (latency > performance.MaxLatency)
                performance.MaxLatency = latency;

            performance.AverageLatency = TimeSpan.FromTicks(
                performance.TotalLatency.Ticks / performance.TotalRequests);

            // performance.SuccessRate = (decimal)performance.SuccessfulRequests / performance.TotalRequests;
            performance.UpdatedAt = DateTime.UtcNow;
        }

        protected virtual async Task RecordUsageAsync(ModelResponse response)
        {
            try
            {
                var usageRecord = new UsageRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    TenantId = response.TenantId,
                    ProjectId = response.ProjectId,
                    UserId = response.UserId,
                    ModelId = response.ModelUsed,
                    Provider = response.Provider,
                    InputTokens = response.InputTokens,
                    OutputTokens = response.OutputTokens,
                    //TotalTokens = response.TotalTokens,
                    TotalCost = response.Cost,
                    Timestamp = response.Timestamp,
                    RequestId = response.RequestId,
                    SessionId = response.SessionId,
                    CreatedAt = DateTime.UtcNow,
                    OperationType = "ChatCompletion"
                };

                await _dbContext.UsageRecords.AddAsync(usageRecord);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording usage for provider: {ProviderId}", ProviderId);
            }
        }

        protected virtual async Task RecordStreamingUsageAsync(StreamingCompletion completion)
        {
            try
            {
                var usageRecord = new UsageRecord
                {
                    Id = Guid.NewGuid().ToString(),
                    ModelId = completion.ModelUsed,
                    Provider = completion.Provider,
                    InputTokens = completion.InputTokens,
                    OutputTokens = completion.OutputTokens,
                    // TotalTokens = completion.InputTokens + completion.OutputTokens,
                    TotalCost = (int)completion.Cost,
                    Currency = "USD",
                    Timestamp = completion.CompletedAt,
                    RequestId = completion.RequestId,
                    SessionId = completion.SessionId,
                    CreatedAt = DateTime.UtcNow,
                    OperationType = "StreamingChatCompletion"
                };

                await _dbContext.UsageRecords.AddAsync(usageRecord);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording streaming usage for provider: {ProviderId}", ProviderId);
            }
        }

        protected virtual async Task UpdateProviderHealthAsync(ProviderHealthStatus status)
        {
            try
            {
                var healthMetric = new ProviderHealth
                {
                    Id = Guid.NewGuid().ToString(),
                    ProviderId = ProviderId,
                    ProviderHealthStatus = status,
                    HealthScore = CalculateHealthScore(status),
                    UptimePercentage = CalculateUptimePercentage(),
                    ResponseTime = await CalculateAverageResponseTimeAsync(),
                    ErrorRate = (int)await CalculateErrorRateAsync(),
                    Timestamp = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    LastCheckedAt = DateTime.UtcNow
                };

                await _dbContext.ProviderHealth.AddAsync(healthMetric);

                // Also update provider's last health status
                var provider = await _dbContext.ModelProviders.FindAsync(ProviderId);
                if (provider != null)
                {
                    provider.LastHealthStatus = status;
                    provider.LastHealthCheck = DateTime.UtcNow;
                    _dbContext.ModelProviders.Update(provider);
                }

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating provider health for provider: {ProviderId}", ProviderId);
            }
        }

        protected virtual async Task UpdateModelUsageAsync(string modelId, int inputTokens, int outputTokens)
        {
            try
            {
                var modelUsage = await _dbContext.UsageRecords
                    .FirstOrDefaultAsync(m => m.ModelId == modelId && m.CreatedAt == DateTime.UtcNow.Date);

                if (modelUsage == null)
                {
                    modelUsage = new UsageRecord
                    {
                        Id = Guid.NewGuid().ToString(),
                        ModelId = modelId,
                        CreatedAt = DateTime.UtcNow.Date,
                        TotalRequests = 0,
                        TotalInputTokens = 0,
                        TotalOutputTokens = 0,
                        TotalCost = 0m,
                        UpdatedAt = DateTime.UtcNow
                    };
                    await _dbContext.UsageRecords.AddAsync(modelUsage);
                }

                modelUsage.TotalRequests++;
                modelUsage.TotalInputTokens += inputTokens;
                modelUsage.TotalOutputTokens += outputTokens;
                modelUsage.UpdatedAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating model usage for model: {ModelId}", modelId);
            }
        }

        protected virtual async Task UpdateModelsInDatabaseAsync(List<ProviderModelInfo> models)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync();

            try
            {
                foreach (var modelInfo in models)
                {
                    var existingModel = await _dbContext.AIModels
                        .FirstOrDefaultAsync(m =>
                            m.ProviderId == ProviderId &&
                            m.ProviderModelId == modelInfo.ModelId);
                    var capabilities = modelInfo.Capabilities;
                    var intScore = capabilities.Where(c => c.IntelligenceScore != 0).FirstOrDefault();

                    if (existingModel == null)
                    {
                        var newModel = new AIModel
                        {
                            Id = Guid.NewGuid().ToString(),
                            ProviderId = ProviderId,
                            ProviderModelId = modelInfo.ModelId,
                            Name = modelInfo.ModelName,
                            DisplayName = modelInfo.DisplayName,
                            Description = modelInfo.Description,
                            IntelligenceScore = intScore.IntelligenceScore,
                            CostPerMillionInputTokens = CalculateCostPerMillionTokens(modelInfo.Pricing?.InputTokenPrice ?? 0),
                            CostPerMillionOutputTokens = CalculateCostPerMillionTokens(modelInfo.Pricing?.OutputTokenPrice ?? 0),
                            ContextWindow = modelInfo.ContextWindow,
                            MaxOutputTokens = modelInfo.MaxOutputTokens,
                            Tier = DetermineModelTier(modelInfo),
                            SupportsStreaming = modelInfo.SupportsStreaming,
                            SupportsFunctionCalling = modelInfo.SupportsFunctionCalling,
                            SupportsAudio = modelInfo.SupportsAudio,
                            IsActive = true,
                            CreatedAt = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow,
                            DeprecationDate = modelInfo.DeprecationDate
                        };

                        await _dbContext.AIModels.AddAsync(newModel);
                    }
                    else
                    {
                        existingModel.Name = modelInfo.ModelName;
                        existingModel.DisplayName = modelInfo.DisplayName;
                        existingModel.Description = modelInfo.Description;
                        existingModel.IntelligenceScore = intScore.IntelligenceScore;
                        existingModel.CostPerMillionInputTokens = CalculateCostPerMillionTokens(
                            modelInfo.Pricing?.InputTokenPrice ?? existingModel.CostPerMillionInputTokens / 1000000m);
                        existingModel.CostPerMillionOutputTokens = CalculateCostPerMillionTokens(
                            modelInfo.Pricing?.OutputTokenPrice ?? existingModel.CostPerMillionOutputTokens / 1000000m);
                        existingModel.ContextWindow = intScore.ContextWindow;
                        existingModel.MaxOutputTokens = intScore.MaxOutputTokens;
                        existingModel.SupportsStreaming = modelInfo.SupportsStreaming;
                        existingModel.SupportsFunctionCalling = intScore.SupportsFunctionCalling;
                        existingModel.SupportsVision = intScore.SupportsVision;
                        existingModel.SupportsAudio = intScore.SupportsAudio;
                        existingModel.DeprecationDate = modelInfo.DeprecationDate;
                        existingModel.LastUpdated = DateTime.UtcNow;

                        _dbContext.AIModels.Update(existingModel);
                    }
                }

                // Mark models that are no longer available as inactive
                var availableModelIds = models.Select(m => m.ModelId).ToList();
                var inactiveModels = await _dbContext.AIModels
                    .Where(m => m.ProviderId == ProviderId && !availableModelIds.Contains(m.ProviderModelId) && m.IsActive)
                    .ToListAsync();

                foreach (var model in inactiveModels)
                {
                    model.IsActive = false;
                    model.LastUpdated = DateTime.UtcNow;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error updating models in database for provider: {ProviderId}", ProviderId);
                throw;
            }
        }

        protected virtual async Task CreateErrorLogAsync(
            string requestId, string operation, string modelId, string status,
            double? durationMs = null, bool? success = null, string error = null)
        {
            try
            {
                var errorLog = new ErrorLog
                {
                    RequestId = requestId,
                    ProviderId = ProviderId,
                    ModelId = modelId,
                    Operation = operation,
                    Status = status,
                    DurationMs = durationMs,
                    Success = success,
                    Error = error,
                    Timestamp = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.ErrorLogs.AddAsync(errorLog);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating audit log for request: {RequestId}", requestId);
            }
        }

        protected virtual async Task HandleRequestErrorAsync(
            Exception ex, string requestId, string operation, string modelId, DateTime startTime)
        {
            var processingTime = DateTime.UtcNow - startTime;

            await RecordFailureAsync(ex);
            await UpdatePerformanceMetricsAsync(processingTime, false, modelId);

            await CreateErrorLogAsync(
                requestId, operation, modelId, "FAILED",
                processingTime.TotalMilliseconds, false, ex.Message);

            _logger.LogError(ex, "Request {RequestId} failed for {Operation} on model {ModelId}",
                requestId, operation, modelId);
        }

        #region Cost Calculation Helper Methods

        protected virtual async Task<PricingInfo> GetModelPricingAsync(string modelId)
        {
            var cacheKey = $"{ProviderId}_pricing_{modelId}";
            if (_cache.TryGetValue(cacheKey, out PricingInfo cachedPricing))
            {
                return cachedPricing;
            }

            var model = await _dbContext.AIModels
                .Include(m => m.PricingInfo)
                .FirstOrDefaultAsync(m => m.ProviderId == ProviderId && m.ProviderModelId == modelId);

            if (model != null && model.PricingInfo != null)
            {
                var pricing = new PricingInfo
                {
                    InputTokenPrice = model.CostPerMillionInputTokens / 1_000_000m,
                    OutputTokenPrice = model.CostPerMillionOutputTokens / 1_000_000m,
                    PricingModel = "Token-based",
                    Currency = "USD"
                };

                _cache.Set(cacheKey, pricing, TimeSpan.FromHours(1));
                return pricing;
            }

            // Fallback to provider defaults
            return new PricingInfo
            {
                InputTokenPrice = _configuration.DefaultInputTokenPrice / 1000m,
                OutputTokenPrice = _configuration.DefaultOutputTokenPrice / 1000m,
                PricingModel = "Default Token-based",
                Currency = "USD"
            };
        }

        protected virtual (decimal InputCost, decimal OutputCost, decimal ServiceCost, decimal TotalCost)
            CalculateCosts(PricingInfo pricing, int inputTokens, int outputTokens)
        {
            var inputCost = pricing.InputTokenPrice * inputTokens;
            var outputCost = pricing.OutputTokenPrice * outputTokens;
            var subtotal = inputCost + outputCost;
            var serviceCost = subtotal * _configuration.ServiceFeePercentage;
            var totalCost = subtotal + serviceCost;

            return (inputCost.Value, outputCost.Value, serviceCost.Value, totalCost.Value);
        }

        protected virtual async Task<decimal> GetEstimationAccuracyAsync(string modelId)
        {
            // Calculate accuracy based on historical estimation vs actual costs
            var recentEstimations = await _dbContext.UsageRecords
                .Where(u => u.ModelId == modelId && u.EstimatedCost != 0 && u.TotalCost > 0)
                .OrderByDescending(u => u.Timestamp)
                .Take(100)
                .ToListAsync();

            if (!recentEstimations.Any())
                return 0.7m; // Default confidence

            var totalError = recentEstimations.Sum(u =>
                Math.Abs((decimal)(u.EstimatedCost - u.TotalCost)) / u.TotalCost);

            var averageError = totalError / recentEstimations.Count;
            var accuracy = 1 - averageError;

            return Math.Max(0.5m, Math.Min(1.0m, accuracy));
        }

        protected virtual async Task<decimal> CalculateActualCostAsync(string streamId)
        {
            // This would calculate actual cost based on aggregated token usage
            // For now, return estimated cost
            return 0.02m;
        }

        #endregion

        #region Health Calculation Helper Methods

        protected virtual int CalculateHealthScore(ProviderHealthStatus status)
        {
            return status switch
            {
                ProviderHealthStatus.Healthy => 100,
                ProviderHealthStatus.Degraded => 70,
                ProviderHealthStatus.Unstable => 30,
                ProviderHealthStatus.Down => 0,
                _ => 50
            };
        }

        protected virtual decimal CalculateUptimePercentage()
        {
            // Calculate uptime based on health metrics from last 24 hours
            var healthyCount = _dbContext.ProviderHealth
                .Count(h => h.ProviderId == ProviderId &&
                           h.CreatedAt > DateTime.UtcNow.AddHours(-24) &&
                           h.ProviderHealthStatus.Equals(ProviderHealthStatus.Healthy));

            var totalCount = _dbContext.ProviderHealth
                .Count(h => h.ProviderId == ProviderId &&
                           h.CreatedAt > DateTime.UtcNow.AddHours(-24));

            return totalCount > 0 ? (decimal)healthyCount / totalCount * 100 : 100m;
        }

        protected virtual async Task<TimeSpan> CalculateAverageResponseTimeAsync()
        {
            var recentMetrics = await _dbContext.PerformanceAnalysis
                .Where(p => p.Provider.Id == ProviderId &&
                           p.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-1))
                .ToListAsync();

            if (!recentMetrics.Any())
                return TimeSpan.FromMilliseconds(1000);

            return TimeSpan.FromMilliseconds(
                recentMetrics.Average(m => m.Latency.TotalMilliseconds));
        }

        protected virtual async Task<decimal> CalculateErrorRateAsync()
        {
            var recentMetrics = await _dbContext.PerformanceAnalysis
                .Where(p => p.Provider.Id == ProviderId &&
                           p.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-1))
                .ToListAsync();

            if (!recentMetrics.Any())
                return 0.01m;

            var failedCount = recentMetrics.Count(m => !m.Success);
            return (decimal)failedCount / recentMetrics.Count;
        }

        #endregion

        #region Model Management Helper Methods

        protected virtual decimal CalculateCostPerMillionTokens(decimal pricePerToken)
        {
            return pricePerToken * 1_000_000m;
        }

        protected virtual ModelTier DetermineModelTier(ProviderModelInfo modelInfo)
        {
            if (modelInfo.Pricing?.MonthlyFee > 100)
                return ModelTier.Enterprise;

            var intScore = modelInfo.Capabilities.Where(c => c.IntelligenceScore >= 80).FirstOrDefault();
            if (intScore != null)
                return ModelTier.Premium;

            if (modelInfo.Pricing?.InputTokenPrice > 0.00001m)
                return ModelTier.Standard;

            return ModelTier.Basic;
        }

        #endregion

        #region Performance Metrics Helper Methods

        ///////////////////////////////////////////////////

        protected class UsageMetric
        {
            public string ModelId { get; set; } = string.Empty;
            public int TotalRequests { get; set; }
            public int TotalTokens { get; set; }
            public decimal TotalCost { get; set; }
        }

        public virtual async Task<PerformancePrediction> GetMetricsAsync()
        {
            try
            {
                // Get recent performance data from database
                var recentMetrics = await _dbContext.PerformanceAnalysis
                    .Where(p => p.Provider.Id == ProviderId && p.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-24))
                    .OrderByDescending(p => p.AnalysisPeriodEnd)
                    .Take(1000)
                    .ToListAsync();

                // Get recent usage data with proper typing
                var recentUsage = await _dbContext.UsageRecords
                    .Where(u => u.Provider == ProviderId && u.Timestamp > DateTime.UtcNow.AddHours(-24))
                    .GroupBy(u => u.ModelId)
                    .Select(g => new UsageMetric
                    {
                        ModelId = g.Key,
                        TotalRequests = g.Count(),
                        TotalTokens = g.Sum(u => u.TotalTokens),
                        TotalCost = g.Sum(u => u.TotalCost)
                    })
                    .ToListAsync();

                if (!recentMetrics.Any() && !recentUsage.Any())
                {
                    return new PerformancePrediction
                    {
                        ProviderId = ProviderId,
                        PredictedLatency = TimeSpan.FromMilliseconds(1000),
                        Confidence = 0.5m,
                        PredictedSuccessRate = 0.95m,
                        EstimatedCostPerRequest = 0.01m,
                        Timestamp = DateTime.UtcNow
                    };
                }

                // Calculate metrics
                var predictions = CalculateAdvancedMetrics(recentMetrics, recentUsage);

                return predictions;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get metrics for provider: {ProviderId}", ProviderId);
                return CreateConservativePrediction();
            }
        }




        /// ///////////////////////////////////////////////////////////////

        protected virtual PerformancePrediction CalculateAdvancedMetrics(
            List<PerformanceAnalysis> metrics, List<UsageMetric> usage)
        {
            if (!metrics.Any())
            {
                return CreateConservativePrediction();
            }

            // Calculate basic statistics
            var latencies = metrics.Select(m => m.Latency.TotalMilliseconds).ToList();
            var avgLatency = latencies.Average();
            var p95Latency = CalculatePercentile(latencies, 95);
            var successRate = (decimal)metrics.Count(m => m.Success) / metrics.Count;

            // Calculate throughput
            var recentHour = metrics.Where(m => m.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-1)).ToList();
            var throughput = recentHour.Count > 0 ? recentHour.Count / 60.0 : 0;

            // Calculate cost metrics
            var avgCostPerRequest = usage.Any() ? usage.Average(u => (decimal)u.TotalCost / u.TotalRequests) : 0.02m;
            var avgTokensPerRequest = usage.Any() ? usage.Average(u => (decimal)u.TotalTokens / u.TotalRequests) : 500;

            // Predict future performance
            var predictedLatency = TimeSpan.FromMilliseconds(avgLatency * 1.1); // Add 10% buffer
            var confidence = CalculatePredictionConfidence(metrics, successRate);

            return new PerformancePrediction
            {
                Id = ProviderId,
                PredictedLatency = predictedLatency,
                P95Latency = TimeSpan.FromMilliseconds(p95Latency),
                Confidence = confidence,
                PredictedSuccessRate = successRate,
                EstimatedCostPerRequest = avgCostPerRequest,
                EstimatedCostPerToken = avgCostPerRequest / (decimal)avgTokensPerRequest,
                EstimatedTokensPerSecond = CalculateTokensPerSecond(metrics, usage),
                ThroughputCapacity = CalculateThroughputCapacity(metrics),
                CurrentLoad = CalculateCurrentLoad(metrics),
                Timestamp = DateTime.UtcNow,
                HistoricalDataPoints = metrics.Count,
                RecentFailures = metrics.Count(m => !m.Success && m.AnalysisPeriodEnd > DateTime.UtcNow.AddMinutes(30)),
                Notes = "Based on recent performance data"
            };
        }

        protected virtual double CalculatePercentile(List<double> values, double percentile)
        {
            if (!values.Any())
                return 0;

            values.Sort();
            var index = (int)Math.Ceiling(percentile / 100.0 * values.Count) - 1;
            return values[Math.Max(0, Math.Min(index, values.Count - 1))];
        }

        protected virtual decimal CalculatePredictionConfidence(List<PerformanceAnalysis> metrics, decimal successRate)
        {
            if (metrics.Count < 10)
                return 0.5m;

            var recentMetrics = metrics.Where(m => m.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-1)).ToList();
            if (recentMetrics.Count < 5)
                return 0.6m;

            // Calculate variance in latencies
            var latencies = recentMetrics.Select(m => m.Latency.TotalMilliseconds).ToList();
            var mean = latencies.Average();
            var variance = latencies.Average(l => Math.Pow(l - mean, 2));
            var stdDev = Math.Sqrt(variance);

            // Higher confidence with lower variance and higher success rate
            var stability = (decimal)Math.Exp(-stdDev / 1000); // Normalize
            var confidence = (stability + successRate) / 2;

            return Math.Max(0.3m, Math.Min(1.0m, confidence));
        }

        protected virtual int CalculateTokensPerSecond(List<PerformanceAnalysis> metrics, List<UsageMetric> usage)
        {
            if (!metrics.Any() || !usage.Any())
                return 1000;

            var totalTokens = usage.Sum(u => (int)u.TotalTokens);
            var totalTime = metrics.Sum(m => m.Latency.TotalSeconds);

            return totalTime > 0 ? (int)(totalTokens / totalTime) : 1000;
        }

        protected virtual int CalculateThroughputCapacity(List<PerformanceAnalysis> metrics)
        {
            if (!metrics.Any())
                return 10;

            var recentHour = metrics.Where(m => m.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-1)).ToList();
            var peakThroughput = recentHour.Count > 0 ? recentHour.Count : 1;

            // Add 20% headroom
            return (int)(peakThroughput * 1.2);
        }

        protected virtual decimal CalculateCurrentLoad(List<PerformanceAnalysis> metrics)
        {
            if (!metrics.Any())
                return 0.5m;

            var last5Minutes = metrics.Where(m => m.AnalysisPeriodEnd > DateTime.UtcNow.AddMinutes(-5)).Count();
            var capacity = CalculateThroughputCapacity(metrics);

            return capacity > 0 ? (decimal)last5Minutes / capacity / 5 : 0.5m; // Normalize to per-minute
        }

        protected virtual PerformancePrediction CreateConservativePrediction()
        {
            return new PerformancePrediction
            {
                ProviderId = ProviderId,
                PredictedLatency = TimeSpan.FromMilliseconds(2000),
                P95Latency = TimeSpan.FromMilliseconds(3000),
                Confidence = 0.3m,
                PredictedSuccessRate = 0.85m,
                EstimatedCostPerRequest = 0.02m,
                EstimatedCostPerToken = 0.000002m,
                EstimatedTokensPerSecond = 1000,
                ThroughputCapacity = 10,
                CurrentLoad = 0.5m,
                Timestamp = DateTime.UtcNow,
                HistoricalDataPoints = 0,
                RecentFailures = 0,
                Notes = "Conservative estimates due to insufficient data"
            };
        }

        #endregion

        #region Fallback Methods

        protected virtual CostEstimation CreateFallbackCostEstimation(ChatRequest request)
        {
            var inputTokens = EstimateInputTokens(request);
            var outputTokens = EstimateOutputTokens(request);
            var totalTokens = inputTokens + outputTokens;

            return new CostEstimation
            {
                EstimatedCost = 0.02m,
                InputCost = 0.01m,
                OutputCost = 0.01m,
                EstimatedInputTokens = inputTokens,
                EstimatedOutputTokens = outputTokens,
                TotalTokens = totalTokens,
                Currency = "USD",
                Confidence = 0.3m,
                PricingModel = "Fallback",
                Notes = "Fallback estimation due to error",
                ModelId = request.ModelId,
                ProviderId = ProviderId,
                Timestamp = DateTime.UtcNow,
                IsDetailedEstimation = false
            };
        }

        protected virtual CostEstimation CreateFallbackCostEstimation(int inputTokens, int outputTokens)
        {
            return new CostEstimation
            {
                EstimatedCost = 0.02m,
                InputCost = 0.01m,
                OutputCost = 0.01m,
                EstimatedInputTokens = inputTokens,
                EstimatedOutputTokens = outputTokens,
                TotalTokens = inputTokens + outputTokens,
                Currency = "USD",
                Confidence = 0.3m,
                PricingModel = "Fallback",
                Notes = "Fallback estimation due to error",
                ProviderId = ProviderId,
                Timestamp = DateTime.UtcNow,
                IsDetailedEstimation = false
            };
        }

        #endregion

        #region Validation Methods

        protected virtual void ValidateRequest(ChatRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.ModelId))
                throw new ArgumentException("Model ID is required", nameof(request.ModelId));

            if (request.Messages == null || !request.Messages.Any())
                throw new ArgumentException("At least one message is required", nameof(request.Messages));

            if (request.MaxTokens.HasValue && request.MaxTokens.Value <= 0)
                throw new ArgumentException("Max tokens must be greater than 0", nameof(request.MaxTokens));

            if (request.MaxTokens.HasValue && request.MaxTokens.Value > 100000)
                throw new ArgumentException("Max tokens cannot exceed 100,000", nameof(request.MaxTokens));

            if (request.Temperature.HasValue && (request.Temperature.Value < 0 || request.Temperature.Value > 2))
                throw new ArgumentException("Temperature must be between 0 and 2", nameof(request.Temperature));

            if (request.TopP.HasValue && (request.TopP.Value < 0 || request.TopP.Value > 1))
                throw new ArgumentException("TopP must be between 0 and 1", nameof(request.TopP));

            if (request.FrequencyPenalty.HasValue && (request.FrequencyPenalty.Value < -2 || request.FrequencyPenalty.Value > 2))
                throw new ArgumentException("Frequency penalty must be between -2 and 2", nameof(request.FrequencyPenalty));

            if (request.PresencePenalty.HasValue && (request.PresencePenalty.Value < -2 || request.PresencePenalty.Value > 2))
                throw new ArgumentException("Presence penalty must be between -2 and 2", nameof(request.PresencePenalty));
        }

        protected virtual void ValidateEmbeddingRequest(EmbeddingRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.ModelId))
                throw new ArgumentException("Model ID is required", nameof(request.ModelId));

            if (!request.Inputs.Any())
                throw new ArgumentException("Input text is required", nameof(request.Inputs));

            char[] allChars = string.Concat(request.Inputs).ToCharArray();
            if (allChars.Length > 100000)
                throw new ArgumentException("Input text is too long", nameof(request.Inputs));
        }

        protected virtual void ValidateModerationRequest(ModerationRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrEmpty(request.ModelId))
                throw new ArgumentException("Model ID is required", nameof(request.ModelId));

            if (string.IsNullOrEmpty(request.Input))
                throw new ArgumentException("Input text is required", nameof(request.Input));

            if (request.Input.Length > 100000)
                throw new ArgumentException("Input text is too long", nameof(request.Input));
        }

        #endregion

        #region Configuration Change Logging

        protected virtual async Task CreateConfigurationChangeLogAsync(ProviderConfiguration newConfig)
        {
            try
            {
                var changeLog = new ConfigurationChangeLog
                {
                    Id = Guid.NewGuid().ToString(),
                    ProviderId = ProviderId,
                    ChangedBy = "System", // Would be actual user in production
                    ChangeType = "Update",
                    OldConfiguration = JsonSerializer.Serialize(_configuration),
                    NewConfiguration = JsonSerializer.Serialize(newConfig),
                    ChangedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };

                await _dbContext.ConfigurationChangeLogs.AddAsync(changeLog);
                await _dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating configuration change log for provider: {ProviderId}", ProviderId);
            }
        }

        private GeminiGenerateContentRequest MapToGeminiRequest(ChatRequest request)
        {
            var contents = new List<GeminiContent>();

            // Gemini uses a flat list of alternating user/assistant messages
            foreach (var msg in request.Messages)
            {
                contents.Add(new GeminiContent
                {
                    Role = msg.Role == ChatRole.Assistant ? "model" : "user",
                    Parts = new List<GeminiPart>
                {
                    new GeminiPart { Text = msg.Content }
                }
                });
            }

            return new GeminiGenerateContentRequest
            {
                Contents = contents,
                GenerationConfig = new GeminiGenerationConfig
                {
                    Temperature = (float?)request.Temperature,
                    TopP = (float?)request.TopP,
                    MaxOutputTokens = (int?)request.MaxTokens
                },
                SafetySettings = GetSafetySettings()
            };
        }
        #endregion
    }

}
#endregion
#endregion


//public virtual async Task<PerformancePrediction> GetMetricsAsync()
//{
//    var cacheKey = $"{ProviderId}_metrics";
//    if (_cache.TryGetValue(cacheKey, out PerformancePrediction cachedMetrics))
//    {
//        return cachedMetrics;
//    }

//    try
//    {
//        // Get recent performance data from database
//        var recentMetrics = await _dbContext.PerformanceAnalysis
//            .Where(p => p.Provider.Id == ProviderId && p.AnalysisPeriodEnd > DateTime.UtcNow.AddHours(-24))
//            .OrderByDescending(p => p.AnalysisPeriodEnd)
//            .Take(1000)
//            .ToListAsync();

//        // Get recent usage data
//        var recentUsage = await _dbContext.UsageRecords
//            .Where(u => u.Provider == ProviderId && u.Timestamp > DateTime.UtcNow.AddHours(-24))
//            .GroupBy(u => u.ModelId)
//            .Select(g => new
//            {
//                ModelId = g.Key,
//                TotalRequests = g.Count(),
//                TotalTokens = g.Sum(u => u.TotalTokens),
//                TotalCost = g.Sum(u => u.Cost)
//            })
//            .ToListAsync();

//        // Calculate advanced metrics
//        var predictions = CalculateAdvancedMetrics(recentMetrics, recentUsage);

//        // Cache the results
//        _cache.Set(cacheKey, predictions, TimeSpan.FromMinutes(5));

//        return predictions;
//    }
//    catch (Exception ex)
//    {
//        _logger.LogError(ex, "Failed to get metrics for provider: {ProviderId}", ProviderId);

//        // Return conservative fallback metrics
//        return new PerformancePrediction
//        {
//            ProviderId = ProviderId,
//            PredictedLatency = TimeSpan.FromMilliseconds(2000),
//            Confidence = 0.5m,
//            PredictedSuccessRate = 0.85m,
//            EstimatedCostPerRequest = 0.02m,
//            EstimatedCostPerToken = 0.000002m,
//            EstimatedTokensPerSecond = 1000,
//            ThroughputCapacity = 10,
//            CurrentLoad = 0.7m,
//            Timestamp = DateTime.UtcNow,
//            HistoricalDataPoints = 0,
//            RecentFailures = 0,
//            Notes = "Error calculating metrics, using conservative estimates"
//        };
//    }
//}


//public class MockProviderAdapter : BaseProviderAdapter
//{
//    public override string ProviderName => "MockProvider";
//    public override string ProviderId => "mock";
//    public override string BaseUrl => "https://api.mockprovider.com/v1";

//    public MockProviderAdapter(ILogger<MockProviderAdapter> logger, HttpClient httpClient)
//        : base(logger, httpClient)
//    {
//    }

//    protected override HttpRequestMessage PrepareChatRequest(ChatRequest request)
//    {
//        var url = $"{BaseUrl}/chat/completions";
//        var body = new
//        {
//            model = request.ModelId,
//            messages = request.Messages,
//            max_tokens = request.MaxTokens,
//            temperature = request.Temperature
//        };

//        return new HttpRequestMessage(HttpMethod.Post, url)
//        {
//            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
//        };
//    }

//    protected override HttpRequestMessage PrepareStreamingChatRequest(ChatRequest request)
//    {
//        var url = $"{BaseUrl}/chat/completions";
//        var body = new
//        {
//            model = request.ModelId,
//            messages = request.Messages,
//            max_tokens = request.MaxTokens,
//            temperature = request.Temperature,
//            stream = true
//        };

//        return new HttpRequestMessage(HttpMethod.Post, url)
//        {
//            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
//        };
//    }

//    protected override HttpRequestMessage PrepareEmbeddingRequest(EmbeddingRequest request)
//    {
//        var url = $"{BaseUrl}/embeddings";
//        var body = new
//        {
//            model = request.ModelId,
//            input = request.Input
//        };

//        return new HttpRequestMessage(HttpMethod.Post, url)
//        {
//            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
//        };
//    }

//    protected override HttpRequestMessage PrepareModerationRequest(ModerationRequest request)
//    {
//        var url = $"{BaseUrl}/moderations";
//        var body = new
//        {
//            model = request.ModelId,
//            input = request.Input
//        };

//        return new HttpRequestMessage(HttpMethod.Post, url)
//        {
//            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
//        };
//    }

//    protected override async Task<IAsyncEnumerable<string>> SendStreamingRequestAsync(HttpRequestMessage request)
//    {
//        // Simulate a streaming response
//        async IAsyncEnumerable<string> MockStream()
//        {
//            var words = new[] { "Hello, ", "this ", "is ", "a ", "streaming ", "response." };
//            foreach (var word in words)
//            {
//                await Task.Delay(100);
//                yield return word;
//            }
//        }

//        return MockStream();
//    }

//    protected override ModelResponse ProcessChatResponse(HttpResponseMessage response, string modelId)
//    {
//        // Simulate processing a response
//        return new ModelResponse
//        {
//            Id = Guid.NewGuid().ToString(),
//            ModelId = modelId,
//            Content = "This is a mock response from the provider.",
//            InputTokens = 100,
//            OutputTokens = 50,
//            Cost = 0.0015m,
//            ProcessingTime = TimeSpan.FromMilliseconds(500),
//            Success = true
//        };
//    }

//    protected override EmbeddingResponse ProcessEmbeddingResponse(HttpResponseMessage response, string modelId)
//    {
//        return new EmbeddingResponse
//        {
//            Id = Guid.NewGuid().ToString(),
//            ModelId = modelId,
//            Embeddings = new[] { 0.1f, 0.2f, 0.3f },
//            InputTokens = 10,
//            Cost = 0.0001m,
//            ProcessingTime = TimeSpan.FromMilliseconds(100),
//            Success = true
//        };
//    }

//    protected override ModerationResponse ProcessModerationResponse(HttpResponseMessage response, string modelId)
//    {
//        return new ModerationResponse
//        {
//            Id = Guid.NewGuid().ToString(),
//            ModelId = modelId,
//            IsFlagged = false,
//            Categories = new Dictionary<string, bool>(),
//            CategoryScores = new Dictionary<string, float>(),
//            InputTokens = 5,
//            Cost = 0.00005m,
//            ProcessingTime = TimeSpan.FromMilliseconds(50),
//            Success = true
//        };
//    }
//}
