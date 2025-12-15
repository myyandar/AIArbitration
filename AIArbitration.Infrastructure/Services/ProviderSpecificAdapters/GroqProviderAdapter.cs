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
#region Additional Providers

#region Groq Provider Adapter

public class GroqProviderAdapter : BaseProviderAdapter
{
    private readonly Dictionary<string, decimal> _groqPricing;

    public GroqProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<GroqProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
        _groqPricing = InitializeGroqPricing();
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

        var groqRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            stream = false,
            stop = request.StopSequences
        };

        var json = JsonSerializer.Serialize(groqRequest, _jsonOptions);
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

        var groqRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(groqRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        throw new NotSupportedException("Groq does not currently offer embedding models");
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        throw new NotSupportedException("Groq does not have a dedicated moderation endpoint");
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var groqResponse = JsonSerializer.Deserialize<GroqChatResponse>(content, _jsonOptions);

        if (groqResponse == null)
            throw new ProviderException($"Failed to parse Groq response: {content}", ProviderId);

        var choices = groqResponse.Choices.Select((c, index) => new ModelChoice
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
            Id = groqResponse.Id,
            ModelUsed = groqResponse.Model,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = groqResponse.Usage.PromptTokens,
            OutputTokens = groqResponse.Usage.CompletionTokens,
            TotalTokens = groqResponse.Usage.TotalTokens,
            Cost = await CalculateCostAsync(groqResponse.Model, groqResponse.Usage.PromptTokens, groqResponse.Usage.CompletionTokens),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            Created = groqResponse.Created,
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
        throw new NotSupportedException("Groq does not currently offer embedding models");
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        throw new NotSupportedException("Groq does not have a dedicated moderation endpoint");
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "llama3-70b-8192",
                ModelName = "Llama 3 70B",
                DisplayName = "Llama 3 70B",
                Description = "Meta's Llama 3 70B model, optimized for speed",
                Provider = ProviderName,
                ContextWindow = 8192,
                MaxOutputTokens = 2048,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000059m, // $0.59 per 1M tokens
                    OutputTokenPrice = 0.00000079m, // $0.79 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 84 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 81 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "mixtral-8x7b-32768",
                ModelName = "Mixtral 8x7B",
                DisplayName = "Mixtral 8x7B",
                Description = "Mixture of Experts model with 32K context",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = false,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000024m, // $0.24 per 1M tokens
                    OutputTokenPrice = 0.00000024m, // $0.24 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 79 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 76 }
                }
            }
        };
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        throw new NotSupportedException("Groq does not have a model info endpoint");
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

    #region Groq-specific Helper Methods

    private Dictionary<string, decimal> InitializeGroqPricing()
    {
        return new Dictionary<string, decimal>
        {
            ["llama3-70b-8192"] = 0.59m,
            ["mixtral-8x7b-32768"] = 0.24m,
            ["gemma-7b-it"] = 0.10m
        };
    }

    private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
    {
        var basePrice = _groqPricing.TryGetValue(modelId, out var price) ? price : 0.59m;
        var inputCost = inputTokens * (basePrice / 1000000m);
        var outputCost = outputTokens * ((basePrice * 1.33m) / 1000000m); // Output is ~1.33x input price
        var totalCost = inputCost + outputCost;

        if (_configuration.ServiceFeePercentage > 0)
        {
            totalCost += totalCost * _configuration.ServiceFeePercentage;
        }

        return totalCost;
    }

    #endregion

    #region Groq Response Classes

    private class GroqChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string Model { get; set; } = string.Empty;
        public List<GroqChoice> Choices { get; set; } = new();
        public GroqUsage Usage { get; set; } = new();
    }

    private class GroqChoice
    {
        public int Index { get; set; }
        public GroqMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    private class GroqMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class GroqUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    #endregion
}

#endregion
#region Ollama Provider Adapter (Self-hosted)

#endregion

#endregion