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
#region Mistral Provider Adapter

public class MistralProviderAdapter : BaseProviderAdapter
{
    private readonly Dictionary<string, decimal> _mistralPricing;

    public MistralProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<MistralProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
        _mistralPricing = InitializeMistralPricing();
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

        var mistralRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            stream = false,
            random_seed = request.Seed
        };

        var json = JsonSerializer.Serialize(mistralRequest, _jsonOptions);
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

        var mistralRequest = new
        {
            model = request.ModelId,
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(mistralRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        var url = $"{BaseUrl}/embeddings";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var mistralRequest = new
        {
            model = request.ModelId,
            input = request.Inputs
        };

        var json = JsonSerializer.Serialize(mistralRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        throw new NotSupportedException("Mistral does not have a dedicated moderation endpoint");
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var mistralResponse = JsonSerializer.Deserialize<MistralChatResponse>(content, _jsonOptions);

        if (mistralResponse == null)
            throw new ProviderException($"Failed to parse Mistral response: {content}", ProviderId);

        var choices = mistralResponse.Choices.Select((c, index) => new ModelChoice
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
            Id = mistralResponse.Id,
            ModelUsed = mistralResponse.Model,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = mistralResponse.Usage.PromptTokens,
            OutputTokens = mistralResponse.Usage.CompletionTokens,
            TotalTokens = mistralResponse.Usage.TotalTokens,
            Cost = await CalculateCostAsync(mistralResponse.Model, mistralResponse.Usage.PromptTokens, mistralResponse.Usage.CompletionTokens),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            Created = mistralResponse.Created,
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
        var content = await response.Content.ReadAsStringAsync();
        var mistralResponse = JsonSerializer.Deserialize<MistralEmbeddingResponse>(content, _jsonOptions);

        if (mistralResponse == null)
            throw new ProviderException($"Failed to parse Mistral embedding response: {content}", ProviderId);

        var embeddings = mistralResponse.Data.Select((d, i) => new EmbeddingData
        {
            Index = i,
            Embedding = d.Embedding
        }).ToList();

        return new EmbeddingResponse
        {
            Model = mistralResponse.Model,
            Data = embeddings,
            InputTokens = mistralResponse.Usage.PromptTokens,
            Cost = await CalculateCostAsync(mistralResponse.Model, mistralResponse.Usage.PromptTokens, 0),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        throw new NotSupportedException("Mistral does not have a dedicated moderation endpoint");
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        // Mistral models - static list
        return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "mistral-large-latest",
                ModelName = "Mistral Large",
                DisplayName = "Mistral Large",
                Description = "Top-tier reasoning capabilities, ideal for complex tasks",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 16384,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000008m, // $8 per 1M tokens
                    OutputTokenPrice = 0.000024m, // $24 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 91 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 89 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 87 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "mistral-medium-latest",
                ModelName = "Mistral Medium",
                DisplayName = "Mistral Medium",
                Description = "Excellent balance of performance and cost",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 8192,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000027m, // $2.70 per 1M tokens
                    OutputTokenPrice = 0.0000081m, // $8.10 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 85 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 83 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 80 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "mistral-small-latest",
                ModelName = "Mistral Small",
                DisplayName = "Mistral Small",
                Description = "Cost-effective model for everyday tasks",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 8192,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000001m, // $1 per 1M tokens
                    OutputTokenPrice = 0.000003m, // $3 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 78 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 76 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 75 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "codestral-latest",
                ModelName = "Codestral",
                DisplayName = "Codestral",
                Description = "Specialized code generation model",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 16384,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000001m, // $1 per 1M tokens
                    OutputTokenPrice = 0.000003m, // $3 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 92 },
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 80 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 85 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "mistral-embed",
                ModelName = "Mistral Embed",
                DisplayName = "Mistral Embed",
                Description = "Embedding model for retrieval tasks",
                Provider = ProviderName,
                ContextWindow = 8192,
                MaxOutputTokens = 0,
                SupportsStreaming = false,
                SupportsFunctionCalling = false,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000001m, // $0.10 per 1M tokens
                    OutputTokenPrice = 0m,
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextEmbedding, Score = 86 }
                }
            }
        };
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var modelInfo = JsonSerializer.Deserialize<MistralModelInfo>(content, _jsonOptions);

        if (modelInfo == null)
            throw new ProviderException($"Failed to parse Mistral model info response: {content}", ProviderId);

        return new ProviderModelInfo
        {
            ModelId = modelInfo.Id,
            ModelName = modelInfo.Id,
            DisplayName = FormatMistralModelName(modelInfo.Id),
            Description = $"Mistral {modelInfo.Id} model",
            Provider = ProviderName,
            ContextWindow = GetMistralContextWindow(modelInfo.Id),
            MaxOutputTokens = GetMistralMaxOutputTokens(modelInfo.Id),
            SupportsStreaming = modelInfo.Permissions?.Contains("streaming") == true,
            SupportsFunctionCalling = modelInfo.Id.Contains("large") || modelInfo.Id.Contains("medium"),
            IsActive = modelInfo.Object == "model",
            Pricing = new PricingInfo
            {
                InputTokenPrice = GetMistralInputTokenPrice(modelInfo.Id),
                OutputTokenPrice = GetMistralOutputTokenPrice(modelInfo.Id),
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

    #region Mistral-specific Helper Methods

    private Dictionary<string, decimal> InitializeMistralPricing()
    {
        return new Dictionary<string, decimal>
        {
            ["mistral-large-latest"] = 8.0m, // per 1M input tokens
            ["mistral-medium-latest"] = 2.7m,
            ["mistral-small-latest"] = 1.0m,
            ["codestral-latest"] = 1.0m,
            ["mistral-embed"] = 0.1m
        };
    }

    private string FormatMistralModelName(string modelId)
    {
        return modelId.Replace("-", " ").ToUpperInvariant();
    }

    private int GetMistralContextWindow(string modelId)
    {
        if (modelId.Contains("large") || modelId.Contains("medium") || modelId.Contains("small") || modelId.Contains("codestral"))
            return 32768;
        if (modelId.Contains("embed"))
            return 8192;
        return 16384;
    }

    private int GetMistralMaxOutputTokens(string modelId)
    {
        if (modelId.Contains("large") || modelId.Contains("codestral"))
            return 16384;
        return 8192;
    }

    private decimal GetMistralInputTokenPrice(string modelId)
    {
        var basePrice = _mistralPricing.TryGetValue(modelId, out var price) ? price : 1.0m;
        return basePrice / 1000000m;
    }

    private decimal GetMistralOutputTokenPrice(string modelId)
    {
        var basePrice = _mistralPricing.TryGetValue(modelId, out var price) ? price : 1.0m;
        return (basePrice * 3) / 1000000m; // Output is 3x input price
    }

    private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
    {
        var inputCost = inputTokens * GetMistralInputTokenPrice(modelId);
        var outputCost = outputTokens * GetMistralOutputTokenPrice(modelId);
        var totalCost = inputCost + outputCost;

        // Add service fee
        if (_configuration.ServiceFeePercentage > 0)
        {
            totalCost += totalCost * _configuration.ServiceFeePercentage;
        }

        return totalCost;
    }

    #endregion

    #region Mistral Response Classes

    private class MistralChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string Model { get; set; } = string.Empty;
        public List<MistralChoice> Choices { get; set; } = new();
        public MistralUsage Usage { get; set; } = new();
    }

    private class MistralChoice
    {
        public int Index { get; set; }
        public MistralMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    private class MistralMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class MistralUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    private class MistralEmbeddingResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public List<MistralEmbeddingData> Data { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public MistralUsage Usage { get; set; } = new();
    }

    private class MistralEmbeddingData
    {
        public int Index { get; set; }
        public List<float> Embedding { get; set; } = new();
        public string Object { get; set; } = string.Empty;
    }

    private class MistralModelInfo
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string OwnedBy { get; set; } = string.Empty;
        public List<string>? Permissions { get; set; }
    }

    #endregion
}

#endregion
#region DeepSeek Provider Adapter

#endregion
#region Microsoft Azure OpenAI Provider Adapter

#endregion
#region Additional Providers
#region Groq Provider Adapter

#endregion
#region Ollama Provider Adapter (Self-hosted)

#endregion

#endregion