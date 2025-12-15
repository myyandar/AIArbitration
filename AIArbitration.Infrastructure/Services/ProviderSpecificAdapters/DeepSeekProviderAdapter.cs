using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using AIArbitration.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
#region DeepSeek Provider Adapter

public class DeepSeekProviderAdapter : BaseProviderAdapter
{
    private readonly Dictionary<string, decimal> _deepseekPricing;

    public DeepSeekProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<DeepSeekProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
        _deepseekPricing = InitializeDeepSeekPricing();
    }

    protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList();

        var deepseekRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            frequency_penalty = request.FrequencyPenalty,
            presence_penalty = request.PresencePenalty,
            stream = false,
            stop = request.StopSequences
        };

        var json = JsonSerializer.Serialize(deepseekRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/chat/completions";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList();

        var deepseekRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(deepseekRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        throw new NotSupportedException("DeepSeek does not currently offer embedding models");
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        throw new NotSupportedException("DeepSeek does not have a dedicated moderation endpoint");
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var deepseekResponse = JsonSerializer.Deserialize<DeepSeekChatResponse>(content, _jsonOptions);

        if (deepseekResponse == null)
            throw new ProviderException($"Failed to parse DeepSeek response: {content}", ProviderId);

        var choices = deepseekResponse.Choices.Select((c, index) => new ModelChoice
        {
            Index = index,
            Message = new ChatMessage
            {
                Role = c.Message.Role,
                Content = c.Message.Content
            },
            FinishReason = c.FinishReason
        }).ToList();

        return new ModelResponse
        {
            Id = deepseekResponse.Id,
            ModelUsed = deepseekResponse.Model,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = deepseekResponse.Usage.PromptTokens,
            OutputTokens = deepseekResponse.Usage.CompletionTokens,
            TotalTokens = deepseekResponse.Usage.TotalTokens,
            Cost = await CalculateCostAsync(deepseekResponse.Model, deepseekResponse.Usage.PromptTokens, deepseekResponse.Usage.CompletionTokens),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            Created = deepseekResponse.Created,
            RequestId = originalRequest.Id,
            SessionId = originalRequest.SessionId,
            TenantId = originalRequest.TenantId,
            UserId = originalRequest.UserId,
            ProjectId = originalRequest.ProjectId
        };
    }

    protected override async Task<EmbeddingResponse> ParseEmbeddingResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        throw new NotSupportedException("DeepSeek does not currently offer embedding models");
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        throw new NotSupportedException("DeepSeek does not have a dedicated moderation endpoint");
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        // DeepSeek models - static list
        return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "deepseek-chat",
                ModelName = "DeepSeek Chat",
                DisplayName = "DeepSeek Chat",
                Description = "General purpose chat model with strong reasoning capabilities",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                SupportsAudio = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000014m, // $0.14 per 1M tokens
                    OutputTokenPrice = 0.00000028m, // $0.28 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 87 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 85 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 83 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "deepseek-coder",
                ModelName = "DeepSeek Coder",
                DisplayName = "DeepSeek Coder",
                Description = "Specialized code generation model with 128K context",
                Provider = ProviderName,
                ContextWindow = 131072,
                MaxOutputTokens = 8192,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                SupportsAudio = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000014m, // $0.14 per 1M tokens
                    OutputTokenPrice = 0.00000028m, // $0.28 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 90 },
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 80 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 82 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "deepseek-reasoner",
                ModelName = "DeepSeek Reasoner",
                DisplayName = "DeepSeek Reasoner",
                Description = "Specialized reasoning model for complex problem solving",
                Provider = ProviderName,
                ContextWindow = 65536,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                SupportsAudio = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000028m, // $0.28 per 1M tokens
                    OutputTokenPrice = 0.00000056m, // $0.56 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 89 },
                    new ModelCapability { CapabilityType = CapabilityType.RAG, Score = 88 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 85 }
                }
            }
        };
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var modelInfo = JsonSerializer.Deserialize<DeepSeekModelInfo>(content, _jsonOptions);

        if (modelInfo == null)
            throw new ProviderException($"Failed to parse DeepSeek model info response: {content}", ProviderId);

        return new ProviderModelInfo
        {
            ModelId = modelInfo.Id,
            ModelName = modelInfo.Id,
            DisplayName = FormatDeepSeekModelName(modelInfo.Id),
            Description = $"DeepSeek {modelInfo.Id} model",
            Provider = ProviderName,
            ContextWindow = GetDeepSeekContextWindow(modelInfo.Id),
            MaxOutputTokens = GetDeepSeekMaxOutputTokens(modelInfo.Id),
            SupportsStreaming = modelInfo.Object == "model",
            SupportsFunctionCalling = true, // DeepSeek models support function calling
            IsActive = modelInfo.Object == "model",
            Pricing = new PricingInfo
            {
                InputTokenPrice = GetDeepSeekInputTokenPrice(modelInfo.Id),
                OutputTokenPrice = GetDeepSeekOutputTokenPrice(modelInfo.Id),
                PricingModel = "per-token"
            }
        };
    }

    protected override ProviderHealthStatus ParseHealthCheckResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return ProviderHealthStatus.Healthy;
        }

        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => ProviderHealthStatus.RateLimited,
            System.Net.HttpStatusCode.ServiceUnavailable => ProviderHealthStatus.Down,
            System.Net.HttpStatusCode.InternalServerError => ProviderHealthStatus.Degraded,
            _ => ProviderHealthStatus.Unstable
        };
    }

    protected override async Task<HttpRequestMessage> CreateHealthCheckRequestAsync()
    {
        var url = $"{BaseUrl}/models";
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
    {
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
    }

    #region DeepSeek-specific Helper Methods

    private Dictionary<string, decimal> InitializeDeepSeekPricing()
    {
        return new Dictionary<string, decimal>
        {
            ["deepseek-chat"] = 0.14m, // per 1M input tokens
            ["deepseek-coder"] = 0.14m,
            ["deepseek-reasoner"] = 0.28m
        };
    }

    private string FormatDeepSeekModelName(string modelId)
    {
        return modelId.Replace("-", " ").ToUpperInvariant();
    }

    private int GetDeepSeekContextWindow(string modelId)
    {
        return modelId switch
        {
            "deepseek-chat" => 32768,
            "deepseek-coder" => 131072,
            "deepseek-reasoner" => 65536,
            _ => 32768
        };
    }

    private int GetDeepSeekMaxOutputTokens(string modelId)
    {
        return modelId switch
        {
            "deepseek-coder" => 8192,
            _ => 4096
        };
    }

    private decimal GetDeepSeekInputTokenPrice(string modelId)
    {
        var basePrice = _deepseekPricing.TryGetValue(modelId, out var price) ? price : 0.14m;
        return basePrice / 1000000m;
    }

    private decimal GetDeepSeekOutputTokenPrice(string modelId)
    {
        var basePrice = _deepseekPricing.TryGetValue(modelId, out var price) ? price : 0.14m;
        return (basePrice * 2) / 1000000m; // Output is 2x input price
    }

    private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
    {
        var inputCost = inputTokens * GetDeepSeekInputTokenPrice(modelId);
        var outputCost = outputTokens * GetDeepSeekOutputTokenPrice(modelId);
        var totalCost = inputCost + outputCost;

        // Add service fee
        if (_configuration.ServiceFeePercentage > 0)
        {
            totalCost += totalCost * _configuration.ServiceFeePercentage;
        }

        return totalCost;
    }

    #endregion

    #region DeepSeek Response Classes

    private class DeepSeekChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string Model { get; set; } = string.Empty;
        public List<DeepSeekChoice> Choices { get; set; } = new();
        public DeepSeekUsage Usage { get; set; } = new();
    }

    private class DeepSeekChoice
    {
        public int Index { get; set; }
        public DeepSeekMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    private class DeepSeekMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class DeepSeekUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    private class DeepSeekModelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string OwnedBy { get; set; } = string.Empty;
    }

    #endregion
}

#endregion
#region Microsoft Azure OpenAI Provider Adapter

#endregion
#region Additional Providers
#region Groq Provider Adapter

#endregion
#region Ollama Provider Adapter (Self-hosted)

#endregion

#endregion