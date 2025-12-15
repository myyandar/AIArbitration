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
#region Microsoft Azure OpenAI Provider Adapter

public class MicrosoftAzureOpenAiProviderAdapter : BaseProviderAdapter
{
    private readonly string _apiVersion = "2024-02-01";
    private readonly Dictionary<string, decimal> _azureOpenAiPricing;

    public MicrosoftAzureOpenAiProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<MicrosoftAzureOpenAiProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
        _azureOpenAiPricing = InitializeAzureOpenAiPricing();
    }

    protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
    {
        var deploymentName = GetDeploymentName(request.ModelId);
        var url = $"{BaseUrl}/openai/deployments/{deploymentName}/chat/completions?api-version={_apiVersion}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content,
            name = m.Name,
            function_call = m.FunctionCall != null ? new
            {
                name = m.FunctionCall.Name,
                arguments = m.FunctionCall.Arguments
            } : null
        }).ToList();

        var azureRequest = new
        {
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            top_p = request.TopP,
            frequency_penalty = request.FrequencyPenalty,
            presence_penalty = request.PresencePenalty,
            stop = request.StopSequences?.Any() == true ? request.StopSequences : null,
            stream = false,
            user = request.UserId,
            functions = request.Tools?.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.Parameters
            }).ToList(),
            function_call = request.ToolChoice
        };

        var json = JsonSerializer.Serialize(azureRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
    {
        var deploymentName = GetDeploymentName(request.ModelId);
        var url = $"{BaseUrl}/openai/deployments/{deploymentName}/chat/completions?api-version={_apiVersion}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var messages = request.Messages.Select(m => new
        {
            role = m.Role,
            content = m.Content
        }).ToList();

        var azureRequest = new
        {
            messages = messages,
            temperature = request.Temperature,
            max_tokens = request.MaxTokens,
            stream = true
        };

        var json = JsonSerializer.Serialize(azureRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        var deploymentName = GetDeploymentName(request.ModelId);
        var url = $"{BaseUrl}/openai/deployments/{deploymentName}/embeddings?api-version={_apiVersion}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var azureRequest = new
        {
            input = request.Inputs,
            user = request.UserId
        };

        var json = JsonSerializer.Serialize(azureRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        var deploymentName = GetDeploymentName(request.ModelId);
        var url = $"{BaseUrl}/openai/deployments/{deploymentName}/moderations?api-version={_apiVersion}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var azureRequest = new
        {
            input = request.Input
        };

        var json = JsonSerializer.Serialize(azureRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var azureResponse = JsonSerializer.Deserialize<AzureOpenAiChatResponse>(content, _jsonOptions);

        if (azureResponse == null)
            throw new ProviderException($"Failed to parse Azure OpenAI response: {content}", ProviderId);

        var choices = azureResponse.Choices.Select((c, index) => new ModelChoice
        {
            Index = index,
            Message = new ChatMessage
            {
                Role = c.Message.Role,
                Content = c.Message.Content,
                Name = c.Message.Name,
                FunctionCall = c.Message.FunctionCall != null ? new FunctionCall
                {
                    Name = c.Message.FunctionCall.Name,
                    Arguments = c.Message.FunctionCall.Arguments
                } : null
            },
            FinishReason = c.FinishReason
        }).ToList();

        return new ModelResponse
        {
            Id = azureResponse.Id,
            ModelUsed = originalRequest.ModelId,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = azureResponse.Usage.PromptTokens,
            OutputTokens = azureResponse.Usage.CompletionTokens,
            TotalTokens = azureResponse.Usage.TotalTokens,
            Cost = await CalculateCostAsync(originalRequest.ModelId, azureResponse.Usage.PromptTokens, azureResponse.Usage.CompletionTokens),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            Created = azureResponse.Created,
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
        var azureResponse = JsonSerializer.Deserialize<AzureOpenAiEmbeddingResponse>(content, _jsonOptions);

        if (azureResponse == null)
            throw new ProviderException($"Failed to parse Azure OpenAI embedding response: {content}", ProviderId);

        var embeddings = azureResponse.Data.Select((d, i) => new EmbeddingData
        {
            Index = i,
            Embedding = d.Embedding
        }).ToList();

        return new EmbeddingResponse
        {
            Model = azureResponse.Model,
            Data = embeddings,
            InputTokens = azureResponse.Usage.PromptTokens,
            Cost = await CalculateCostAsync(azureResponse.Model, azureResponse.Usage.PromptTokens, 0),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var azureResponse = JsonSerializer.Deserialize<AzureOpenAiModerationResponse>(content, _jsonOptions);

        if (azureResponse == null)
            throw new ProviderException($"Failed to parse Azure OpenAI moderation response: {content}", ProviderId);

        var results = azureResponse.Results.Select(r => new ModerationResult
        {
            Categories = r.Categories.ToDictionary(
                kvp => kvp.Key,
                kvp => new ModerationCategory
                {
                    Flagged = kvp.Value,
                    Score = r.CategoryScores != null && r.CategoryScores.TryGetValue(kvp.Key, out var score) ? score : 0.0,
                    Description = string.Empty
                }
            ),
            CategoryScores = r.CategoryScores?.ToDictionary(
                kvp => kvp.Key,
                kvp => (decimal)kvp.Value
            ) ?? new Dictionary<string, decimal>(),
            Flagged = r.Flagged
        }).ToList();

        return new ModerationResponse
        {
            Id = azureResponse.Id,
            ModelUsed = azureResponse.Model,
            Provider = ProviderName,
            IsFlagged = results.Any(r => r.Flagged),
            CategoryScores = results.SelectMany(r => r.CategoryScores)
                                    .GroupBy(kvp => kvp.Key)
                                    .ToDictionary(g => g.Key, g => g.Average(kvp => kvp.Value)),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime
        };
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        // Azure OpenAI doesn't have a models endpoint, return static list based on common deployments
        return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "gpt-4",
                ModelName = "GPT-4",
                DisplayName = "GPT-4",
                Description = "Most capable GPT-4 model, optimized for chat and complex tasks",
                Provider = ProviderName,
                ContextWindow = 8192,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00003m, // $0.03 per 1K tokens
                    OutputTokenPrice = 0.00006m, // $0.06 per 1K tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 95 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 92 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 94 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "gpt-4-turbo",
                ModelName = "GPT-4 Turbo",
                DisplayName = "GPT-4 Turbo",
                Description = "Latest GPT-4 Turbo model with 128K context",
                Provider = ProviderName,
                ContextWindow = 128000,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00001m, // $0.01 per 1K tokens
                    OutputTokenPrice = 0.00003m, // $0.03 per 1K tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 94 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 91 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 93 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "gpt-35-turbo",
                ModelName = "GPT-3.5 Turbo",
                DisplayName = "GPT-3.5 Turbo",
                Description = "Fast and efficient GPT-3.5 model",
                Provider = ProviderName,
                ContextWindow = 16385,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000015m, // $0.0015 per 1K tokens
                    OutputTokenPrice = 0.000002m, // $0.002 per 1K tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 85 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 80 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 82 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "text-embedding-ada-002",
                ModelName = "Text Embedding Ada 002",
                DisplayName = "Text Embedding Ada 002",
                Description = "Embedding model for text similarity and search",
                Provider = ProviderName,
                ContextWindow = 8191,
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
                    new ModelCapability { CapabilityType = CapabilityType.TextEmbedding, Score = 88 }
                }
            }
        };
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        throw new NotSupportedException("Azure OpenAI does not have a model info endpoint");
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
        // Use deployments endpoint for health check
        var url = $"{BaseUrl}/openai/deployments?api-version={_apiVersion}";
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
    {
        // Azure OpenAI uses API key in header
        request.Headers.Add("api-key", _configuration.ApiKey);

        // Remove Authorization header since Azure uses api-key
        request.Headers.Remove("Authorization");
    }

    #region Azure OpenAI-specific Helper Methods

    private Dictionary<string, decimal> InitializeAzureOpenAiPricing()
    {
        return new Dictionary<string, decimal>
        {
            ["gpt-4"] = 30.0m, // per 1M input tokens
            ["gpt-4-32k"] = 60.0m,
            ["gpt-4-turbo"] = 10.0m,
            ["gpt-35-turbo"] = 1.5m,
            ["gpt-35-turbo-16k"] = 3.0m,
            ["text-embedding-ada-002"] = 0.1m
        };
    }

    private string GetDeploymentName(string modelId)
    {
        // Map model IDs to deployment names
        // In Azure OpenAI, deployments can have custom names
        // This is a simple mapping - in production, you'd want a more sophisticated approach
        return modelId.Replace(".", "").Replace("-", "");
    }

    private decimal GetAzureOpenAiInputTokenPrice(string modelId)
    {
        var basePrice = _azureOpenAiPricing.TryGetValue(modelId, out var price) ? price : 1.5m;
        return basePrice / 1000000m;
    }

    private decimal GetAzureOpenAiOutputTokenPrice(string modelId)
    {
        var basePrice = _azureOpenAiPricing.TryGetValue(modelId, out var price) ? price : 1.5m;
        return modelId.Contains("gpt-4") ? (basePrice * 2) / 1000000m : basePrice / 1000000m;
    }

    private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
    {
        var inputCost = inputTokens * GetAzureOpenAiInputTokenPrice(modelId);
        var outputCost = outputTokens * GetAzureOpenAiOutputTokenPrice(modelId);
        var totalCost = inputCost + outputCost;

        // Add service fee
        if (_configuration.ServiceFeePercentage > 0)
        {
            totalCost += totalCost * _configuration.ServiceFeePercentage;
        }

        return totalCost;
    }

    #endregion

    #region Azure OpenAI Response Classes

    private class AzureOpenAiChatResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Object { get; set; } = string.Empty;
        public long Created { get; set; }
        public string Model { get; set; } = string.Empty;
        public List<AzureOpenAiChoice> Choices { get; set; } = new();
        public AzureOpenAiUsage Usage { get; set; } = new();
    }

    private class AzureOpenAiChoice
    {
        public int Index { get; set; }
        public AzureOpenAiMessage Message { get; set; } = new();
        public string FinishReason { get; set; } = string.Empty;
    }

    private class AzureOpenAiMessage
    {
        public string Role { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Name { get; set; }
        public AzureOpenAiFunctionCall? FunctionCall { get; set; }
    }

    private class AzureOpenAiFunctionCall
    {
        public string Name { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    private class AzureOpenAiUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    private class AzureOpenAiEmbeddingResponse
    {
        public string Object { get; set; } = string.Empty;
        public List<AzureOpenAiEmbeddingData> Data { get; set; } = new();
        public string Model { get; set; } = string.Empty;
        public AzureOpenAiUsage Usage { get; set; } = new();
    }

    private class AzureOpenAiEmbeddingData
    {
        public int Index { get; set; }
        public List<float> Embedding { get; set; } = new();
        public string Object { get; set; } = string.Empty;
    }

    private class AzureOpenAiModerationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public List<AzureOpenAiModerationResult> Results { get; set; } = new();
    }

    private class AzureOpenAiModerationResult
    {
        public bool Flagged { get; set; }
        public Dictionary<string, bool> Categories { get; set; } = new();
        public Dictionary<string, float> CategoryScores { get; set; } = new();
    }

    #endregion
}

#endregion
#region Additional Providers
#region Groq Provider Adapter

#endregion
#region Ollama Provider Adapter (Self-hosted)

#endregion

#endregion