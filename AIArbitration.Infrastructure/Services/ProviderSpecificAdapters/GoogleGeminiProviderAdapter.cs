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
#region Google Gemini Provider Adapter

public class GoogleGeminiProviderAdapter : BaseProviderAdapter
{
    private readonly string _geminiApiVersion = "v1beta";
    private readonly Dictionary<string, decimal> _geminiPricing;

    public GoogleGeminiProviderAdapter(
        AIArbitrationDbContext dbContext,
        IHttpClientFactory httpClientFactory,
        ILogger<GoogleGeminiProviderAdapter> logger,
        ICircuitBreaker circuitBreaker,
        IRateLimiter rateLimiter,
        IMemoryCache cache,
        ModelProvider provider,
        ProviderConfiguration configuration)
        : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
    {
        _geminiPricing = InitializeGeminiPricing();
    }

    protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/{_geminiApiVersion}/models/{request.ModelId}:generateContent?key={_configuration.ApiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var contents = new List<object>();
        foreach (var message in request.Messages)
        {
            var content = new
            {
                role = message.Role == "user" ? "user" : "model",
                parts = new[]
                {
                    new
                    {
                        text = message.Content
                    }
                }
            };
            contents.Add(content);
        }

        var geminiRequest = new
        {
            contents = contents,
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens,
                topP = request.TopP,
                stopSequences = request.StopSequences
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
            }
        };

        var json = JsonSerializer.Serialize(geminiRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
    {
        var url = $"{BaseUrl}/{_geminiApiVersion}/models/{request.ModelId}:streamGenerateContent?key={_configuration.ApiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var contents = new List<object>();
        foreach (var message in request.Messages)
        {
            var content = new
            {
                role = message.Role == "user" ? "user" : "model",
                parts = new[]
                {
                    new
                    {
                        text = message.Content
                    }
                }
            };
            contents.Add(content);
        }

        var geminiRequest = new
        {
            contents = contents,
            generationConfig = new
            {
                temperature = request.Temperature,
                maxOutputTokens = request.MaxTokens
            }
        };

        var json = JsonSerializer.Serialize(geminiRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
    {
        var url = $"{BaseUrl}/{_geminiApiVersion}/models/{request.ModelId}:embedContent?key={_configuration.ApiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var geminiRequest = new
        {
            content = new
            {
                parts = new[]
                {
                    new
                    {
                        text = string.Join(" ", request.Inputs)
                    }
                }
            }
        };

        var json = JsonSerializer.Serialize(geminiRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
    {
        // Gemini doesn't have a dedicated moderation endpoint, but we can use the content generation with safety settings
        var url = $"{BaseUrl}/{_geminiApiVersion}/models/{request.ModelId}:generateContent?key={_configuration.ApiKey}";
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

        var geminiRequest = new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new
                        {
                            text = request.Input
                        }
                    }
                }
            },
            safetySettings = new[]
            {
                new { category = "HARM_CATEGORY_HARASSMENT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_HATE_SPEECH", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_NONE" },
                new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_NONE" }
            }
        };

        var json = JsonSerializer.Serialize(geminiRequest, _jsonOptions);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        return httpRequest;
    }

    protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
        HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(content, _jsonOptions);

        if (geminiResponse == null)
            throw new ProviderException($"Failed to parse Gemini response: {content}", ProviderId);

        var choices = new List<ModelChoice>
        {
            new ModelChoice
            {
                Index = 0,
                Message = new ChatMessage
                {
                    Role = "assistant",
                    Content = geminiResponse.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text ?? string.Empty
                },
                FinishReason = geminiResponse.Candidates?.FirstOrDefault()?.FinishReason ?? "STOP"
            }
        };

        // Estimate tokens (Gemini doesn't provide token counts in response)
        var inputTokens = EstimateTokenCount(originalRequest.Messages.Sum(m => m.Content.Length));
        var outputTokens = EstimateTokenCount(geminiResponse.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text?.Length ?? 0);

        return new ModelResponse
        {
            Id = geminiResponse.Candidates?.FirstOrDefault()?.Content?.Role ?? Guid.NewGuid().ToString(),
            ModelUsed = originalRequest.ModelId,
            Provider = ProviderName,
            Choices = choices,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            TotalTokens = inputTokens + outputTokens,
            Cost = await CalculateCostAsync(originalRequest.ModelId, inputTokens, outputTokens),
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
        var geminiResponse = JsonSerializer.Deserialize<GeminiEmbeddingResponse>(content, _jsonOptions);

        if (geminiResponse == null)
            throw new ProviderException($"Failed to parse Gemini embedding response: {content}", ProviderId);

        var embeddings = new List<EmbeddingData>
        {
            new EmbeddingData
            {
                Index = 0,
                Embedding = geminiResponse.Embedding?.Values ?? new List<float>()
            }
        };

        return new EmbeddingResponse
        {
            Model = "text-embedding-004", // Gemini's embedding model
            Data = embeddings,
            InputTokens = EstimateTokenCount(geminiResponse.Embedding?.Values?.Count ?? 0 * 4), // Rough estimate
            Cost = await CalculateCostAsync("text-embedding-004", EstimateTokenCount(geminiResponse.Embedding?.Values?.Count ?? 0 * 4), 0),
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    protected override async Task<ModerationResponse> ParseModerationResponseAsync(
        HttpResponseMessage response, TimeSpan processingTime)
    {
        var content = await response.Content.ReadAsStringAsync();
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(content, _jsonOptions);

        if (geminiResponse == null)
            throw new ProviderException($"Failed to parse Gemini moderation response: {content}", ProviderId);

        var categories = new Dictionary<string, ModerationCategory>();
        var categoryScores = new Dictionary<string, decimal>();

        var safetyRatings = geminiResponse.Candidates?.FirstOrDefault()?.SafetyRatings;
        if (safetyRatings != null)
        {
            foreach (var rating in safetyRatings)
            {
                var flagged = rating.Probability == "HIGH" || rating.Probability == "MEDIUM";
                double score = rating.Probability switch
                {
                    "HIGH" => 0.9,
                    "MEDIUM" => 0.7,
                    "LOW" => 0.3,
                    _ => 0.1
                };
                categories[rating.Category] = new ModerationCategory
                {
                    Flagged = flagged,
                    Score = score,
                    Description = rating.Category
                };
                categoryScores[rating.Category] = (decimal)score;
            }
        }

        var results = new List<ModerationResult>
        {
            new ModerationResult
            {
                Categories = categories,
                CategoryScores = categoryScores,
                Flagged = safetyRatings?.Any(r => r.Probability == "HIGH") ?? false
            }
        };

        return new ModerationResponse
        {
            ModelUsed = "gemini-pro", // Default moderation model
            Provider = ProviderName,
            IsFlagged = results.Any(r => r.Flagged),
            CategoryScores = categoryScores,
            Timestamp = DateTime.UtcNow,
            ProcessingTime = processingTime
        };
    }

    protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
    {
        // Google Gemini models - static list since API doesn't provide a models endpoint
        return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "gemini-pro",
                ModelName = "Gemini Pro",
                DisplayName = "Gemini Pro",
                Description = "Best model for scaling across a wide range of tasks",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 2048,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000005m, // $0.50 per 1M tokens
                    OutputTokenPrice = 0.0000015m, // $1.50 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 88 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 85 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 82 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "gemini-pro-vision",
                ModelName = "Gemini Pro Vision",
                DisplayName = "Gemini Pro Vision",
                Description = "Multimodal model that understands text and images",
                Provider = ProviderName,
                ContextWindow = 16384,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = false,
                SupportsVision = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000005m, // $0.50 per 1M tokens
                    OutputTokenPrice = 0.0000015m, // $1.50 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 86 },
                    new ModelCapability { CapabilityType = CapabilityType.Vision, Score = 90 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 83 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "gemini-ultra",
                ModelName = "Gemini Ultra",
                DisplayName = "Gemini Ultra",
                Description = "Most capable model for highly complex tasks",
                Provider = ProviderName,
                ContextWindow = 32768,
                MaxOutputTokens = 8192,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000075m, // $7.50 per 1M tokens
                    OutputTokenPrice = 0.0000225m, // $22.50 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 95 },
                    new ModelCapability { CapabilityType = CapabilityType.Vision, Score = 94 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 93 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "text-embedding-004",
                ModelName = "Text Embedding 004",
                DisplayName = "Text Embedding 004",
                Description = "Embedding model for text similarity and retrieval",
                Provider = ProviderName,
                ContextWindow = 2048,
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
                    new ModelCapability { CapabilityType = CapabilityType.TextEmbedding, Score = 89 }
                }
            }
        };
    }

    protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        var modelInfo = JsonSerializer.Deserialize<GeminiModelInfo>(content, _jsonOptions);

        if (modelInfo == null)
            throw new ProviderException($"Failed to parse Gemini model info response: {content}", ProviderId);

        return new ProviderModelInfo
        {
            ModelId = modelInfo.Name,
            ModelName = modelInfo.DisplayName,
            DisplayName = modelInfo.DisplayName,
            Description = modelInfo.Description,
            Provider = ProviderName,
            ContextWindow = GetGeminiContextWindow(modelInfo.Name),
            MaxOutputTokens = GetGeminiMaxOutputTokens(modelInfo.Name),
            SupportsStreaming = modelInfo.SupportedGenerationMethods?.Contains("GENERATE_CONTENT") == true,
            SupportsFunctionCalling = modelInfo.SupportedGenerationMethods?.Contains("GENERATE_CONTENT") == true, // Gemini supports function calling
            SupportsVision = modelInfo.SupportedInputModalities?.Contains("IMAGE") == true,
            IsActive = true,
            Pricing = new PricingInfo
            {
                InputTokenPrice = GetGeminiInputTokenPrice(modelInfo.Name),
                OutputTokenPrice = GetGeminiOutputTokenPrice(modelInfo.Name),
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
        var url = $"{BaseUrl}/{_geminiApiVersion}/models/gemini-pro?key={_configuration.ApiKey}";
        return new HttpRequestMessage(HttpMethod.Get, url);
    }

    protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
    {
        // Google Gemini uses API key in URL, not headers
        request.Headers.Remove("Authorization");
    }

    #region Gemini-specific Helper Methods

    private Dictionary<string, decimal> InitializeGeminiPricing()
    {
        return new Dictionary<string, decimal>
        {
            ["gemini-pro"] = 0.5m, // per 1M input tokens
            ["gemini-pro-vision"] = 0.5m,
            ["gemini-ultra"] = 7.5m,
            ["text-embedding-004"] = 0.1m
        };
    }

    private int EstimateTokenCount(int characterCount)
    {
        // Rough estimate: 4 characters per token
        return (int)Math.Ceiling(characterCount / 4.0);
    }

    private int GetGeminiContextWindow(string modelId)
    {
        return modelId switch
        {
            "gemini-pro" => 32768,
            "gemini-pro-vision" => 16384,
            "gemini-ultra" => 32768,
            "text-embedding-004" => 2048,
            _ => 8192
        };
    }

    private int GetGeminiMaxOutputTokens(string modelId)
    {
        return modelId switch
        {
            "gemini-pro" => 2048,
            "gemini-pro-vision" => 4096,
            "gemini-ultra" => 8192,
            _ => 2048
        };
    }

    private decimal GetGeminiInputTokenPrice(string modelId)
    {
        var basePrice = _geminiPricing.TryGetValue(modelId, out var price) ? price : 0.5m;
        return basePrice / 1000000m;
    }

    private decimal GetGeminiOutputTokenPrice(string modelId)
    {
        var basePrice = _geminiPricing.TryGetValue(modelId, out var price) ? price : 0.5m;
        return (basePrice * 3) / 1000000m; // Output is typically 3x input price
    }

    private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
    {
        var inputCost = inputTokens * GetGeminiInputTokenPrice(modelId);
        var outputCost = outputTokens * GetGeminiOutputTokenPrice(modelId);
        var totalCost = inputCost + outputCost;

        // Add service fee
        if (_configuration.ServiceFeePercentage > 0)
        {
            totalCost += totalCost * _configuration.ServiceFeePercentage;
        }

        return totalCost;
    }

    #endregion

    #region Gemini Response Classes

    private class GeminiGenerateContentResponse
    {
        public List<GeminiCandidate>? Candidates { get; set; }
        public GeminiUsageMetadata? UsageMetadata { get; set; }
    }

    private class GeminiCandidate
    {
        public GeminiContent? Content { get; set; }
        public string FinishReason { get; set; } = string.Empty;
        public List<GeminiSafetyRating>? SafetyRatings { get; set; }
    }

    private class GeminiContent
    {
        public string Role { get; set; } = string.Empty;
        public List<GeminiPart>? Parts { get; set; }
    }

    private class GeminiPart
    {
        public string? Text { get; set; }
    }

    private class GeminiSafetyRating
    {
        public string Category { get; set; } = string.Empty;
        public string Probability { get; set; } = string.Empty;
    }

    private class GeminiUsageMetadata
    {
        public int PromptTokenCount { get; set; }
        public int CandidatesTokenCount { get; set; }
        public int TotalTokenCount { get; set; }
    }

    private class GeminiEmbeddingResponse
    {
        public GeminiEmbedding? Embedding { get; set; }
    }

    private class GeminiEmbedding
    {
        public List<float>? Values { get; set; }
    }

    private class GeminiModelInfo
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string>? SupportedGenerationMethods { get; set; }
        public List<string>? SupportedInputModalities { get; set; }
        public List<string>? SupportedOutputModalities { get; set; }
    }

    #endregion
}

#endregion
#region Mistral Provider Adapter

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