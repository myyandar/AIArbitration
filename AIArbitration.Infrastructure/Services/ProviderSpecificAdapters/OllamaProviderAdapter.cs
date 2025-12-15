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
#region Amazon Bedrock Provider Adapter

#endregion

#region Ollama Provider Adapter (Self-hosted)

public class OllamaProviderAdapter : BaseProviderAdapter
{
    public OllamaProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
    }

    protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/api/chat";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var ollamaRequest = new
        {
            model = request.ModelId,
            messages = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList(),
            options = new
            {
                temperature = request.Temperature,
                num_predict = request.MaxTokens,
                top_p = request.TopP,
                stop = request.StopSequences
            },
            stream = false
        };

        var json = JsonSerializer.Serialize(ollamaRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/api/chat";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var ollamaRequest = new
        {
            model = request.ModelId,
            messages = request.Messages.Select(m => new
            {
                role = m.Role,
                content = m.Content
            }).ToList(),
            options = new
            {
                temperature = request.Temperature,
                num_predict = request.MaxTokens
            },
            stream = true
        };

        var json = JsonSerializer.Serialize(ollamaRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        var url = $"{BaseUrl}/api/embeddings";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var ollamaRequest = new
        {
            model = request.ModelId,
            prompt = string.Join(" ", request.Inputs)
        };

        var json = JsonSerializer.Serialize(ollamaRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        throw new NotSupportedException("Ollama does not have a dedicated moderation endpoint");
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var ollamaResponse = JsonSerializer.Deserialize<OllamaChatResponse>(content, _jsonOptions);

        if (ollamaResponse == null)
            throw new ProviderException($"Failed to parse Ollama response: {content}", ProviderId);

        var choices = new List<ModelChoice>
        {
            new ModelChoice
            {
                Index = 0,
                Message = new ChatMessage
                {
                    Role = "assistant",
                    Content = ollamaResponse.Message?.Content ?? string.Empty
                },
                FinishReason = ollamaResponse.Done ? "stop" : "length"
            }
        };

        return new ModelResponse
        {
            Id = ollamaResponse.Model,
            ModelUsed = ollamaResponse.Model,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = EstimateTokenCount(originalRequest.Messages.Sum(m => m.Content.Length)),
            OutputTokens = EstimateTokenCount(ollamaResponse.Message?.Content?.Length ?? 0),
            TotalTokens = EstimateTokenCount(originalRequest.Messages.Sum(m => m.Content.Length) + (ollamaResponse.Message?.Content?.Length ?? 0)),
            Cost = 0m, // Self-hosted, no cost
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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
        var ollamaResponse = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(content, _jsonOptions);

        if (ollamaResponse == null)
            throw new ProviderException($"Failed to parse Ollama embedding response: {content}", ProviderId);

        var embeddings = new List<EmbeddingData>
        {
            new EmbeddingData
            {
                Index = 0,
                Embedding = ollamaResponse.Embedding ?? new List<float>()
            }
        };

        return new EmbeddingResponse
        {
            Model = ollamaResponse.Model,
            Data = embeddings,
            InputTokens = EstimateTokenCount(ollamaResponse.Embedding?.Count ?? 0 * 4),
            Cost = 0m, // Self-hosted, no cost
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        throw new NotSupportedException("Ollama does not have a dedicated moderation endpoint");
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var ollamaModels = JsonSerializer.Deserialize<OllamaModelsResponse>(content, _jsonOptions);

        var models = new List<ProviderModelInfo>();

        if (ollamaModels?.Models != null)
        {
            foreach (var model in ollamaModels.Models)
            {
                models.Add(new ProviderModelInfo
                {
                    ModelId = model.Name,
                    ModelName = model.Name,
                    DisplayName = FormatOllamaModelName(model.Name),
                    Description = $"Ollama {model.Name} model",
                    Provider = ProviderName,
                    ContextWindow = GetOllamaContextWindow(model.Name),
                    MaxOutputTokens = 4096,
                    SupportsStreaming = true,
                    SupportsFunctionCalling = false,
                    SupportsVision = model.Name.Contains("vision") || model.Name.Contains("llava"),
                    IsActive = true,
                    Pricing = new PricingInfo
                    {
                        InputTokenPrice = 0m, // Self-hosted
                        OutputTokenPrice = 0m,
                        PricingModel = "free"
                    },
                    Capabilities = GetOllamaCapabilities(model.Name)
                });
            }
        }

        return models;
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var modelInfo = JsonSerializer.Deserialize<OllamaModelInfo>(content, _jsonOptions);

        if (modelInfo == null)
            throw new ProviderException($"Failed to parse Ollama model info response: {content}", ProviderId);

        return new ProviderModelInfo
        {
            ModelId = modelInfo.Name,
            ModelName = modelInfo.Name,
            DisplayName = FormatOllamaModelName(modelInfo.Name),
            Description = modelInfo.Details?.Family ?? $"Ollama {modelInfo.Name} model",
            Provider = ProviderName,
            ContextWindow = modelInfo.Details?.ContextSize ?? 4096,
            MaxOutputTokens = 4096,
            SupportsStreaming = true,
            SupportsFunctionCalling = false,
            SupportsVision = modelInfo.Name.Contains("vision") || modelInfo.Name.Contains("llava"),
            IsActive = true,
            Pricing = new PricingInfo
            {
                InputTokenPrice = 0m,
                OutputTokenPrice = 0m,
                PricingModel = "free"
            }
        };
    }

    protected override ProviderHealthStatus ParseHealthCheckResponse(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return ProviderHealthStatus.Healthy;
        }

        return ProviderHealthStatus.Down;
    }

    protected override async Task<HttpRequestMessage> CreateHealthCheckRequestAsync()
    {
        var url = $"{BaseUrl}/api/tags";
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
    {
        // Ollama doesn't require authentication by default
    }

    #region Ollama-specific Helper Methods

    private int EstimateTokenCount(int characterCount)
    {
        return (int)Math.Ceiling(characterCount / 4.0);
    }

    private string FormatOllamaModelName(string modelName)
    {
        return modelName.Replace(":", " ").ToUpperInvariant();
    }

    private int GetOllamaContextWindow(string modelName)
    {
        if (modelName.Contains("llama3") && modelName.Contains("70b")) return 8192;
        if (modelName.Contains("llama3")) return 8192;
        if (modelName.Contains("mixtral")) return 32768;
        if (modelName.Contains("codellama")) return 16384;
        return 4096;
    }

    private List<ModelCapability> GetOllamaCapabilities(string modelName)
    {
        var capabilities = new List<ModelCapability>();

        if (modelName.Contains("codellama") || modelName.Contains("code"))
        {
            capabilities.Add(new ModelCapability
            {
                CapabilityType = CapabilityType.CodeGeneration,
                Score = modelName.Contains("34b") ? 85 : 80
            });
        }

        if (modelName.Contains("llava") || modelName.Contains("vision"))
        {
            capabilities.Add(new ModelCapability
            {
                CapabilityType = CapabilityType.Vision,
                Score = 75
            });
        }

        capabilities.Add(new ModelCapability
        {
            CapabilityType = CapabilityType.TextGeneration,
            Score = modelName.Contains("70b") ? 82 :
                   modelName.Contains("34b") ? 80 :
                   modelName.Contains("13b") ? 78 : 75
        });

        return capabilities;
    }

    #endregion

    #region Ollama Response Classes

    private class OllamaChatResponse
    {
        public string Model { get; set; } = string.Empty;
        public OllamaMessage? Message { get; set; }
        public bool Done { get; set; }
    }

    private class OllamaMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private class OllamaEmbeddingResponse
    {
        public string Model { get; set; } = string.Empty;
        public List<float>? Embedding { get; set; }
    }

    private class OllamaModelsResponse
    {
        public List<OllamaModel> Models { get; set; } = new();
    }

    private class OllamaModel
    {
        public string Name { get; set; } = string.Empty;
        public string ModifiedAt { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    private class OllamaModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public OllamaModelDetails? Details { get; set; }
    }

    private class OllamaModelDetails
    {
        public string Family { get; set; } = string.Empty;
        public int ContextSize { get; set; }
    }

    #endregion
}

#endregion

