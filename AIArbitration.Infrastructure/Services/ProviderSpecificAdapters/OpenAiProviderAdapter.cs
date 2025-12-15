using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Buffers.Text;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{

    #region OpenAI Provider Adapter

    public class OpenAiProviderAdapter : BaseProviderAdapter
    {
        private readonly IMemoryCache _cache;
        private readonly Dictionary<string, decimal> _openAiPricing;

        public OpenAiProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<OpenAiProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider provider,
            ProviderConfiguration configuration)
            : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
        {
            _cache = cache;
            _openAiPricing = InitializeOpenAiPricing();
        }

        protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/chat/completions";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var openAiRequest = new
            {
                model = request.ModelId,
                messages = request.Messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content,
                    name = m.Name,
                    function_call = m.FunctionCall != null ? new
                    {
                        name = m.FunctionCall.Name,
                        arguments = m.FunctionCall.Arguments
                    } : null,
                    tool_calls = m.ToolCalls?.Select(t => new
                    {
                        id = t.Id,
                        type = "function",
                        function = new
                        {
                            name = t.Function?.Name,
                            arguments = t.Function?.Arguments
                        }
                    })
                }).ToList(),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                top_p = request.TopP,
                frequency_penalty = request.FrequencyPenalty,
                presence_penalty = request.PresencePenalty,
                stop = request.StopSequences?.Any() == true ? request.StopSequences : null,
                stream = false,
                user = request.UserId,
                tools = request.Tools?.Select(t => new
                {
                    type = "function",
                    function = new
                    {
                        name = t.Name,
                        description = t.Description,
                        parameters = t.Parameters
                    }
                }).ToList(),
                tool_choice = request.ToolChoice
            };

            var json = JsonSerializer.Serialize(openAiRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/chat/completions";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var openAiRequest = new
            {
                model = request.ModelId,
                messages = request.Messages.Select(m => new
                {
                    role = m.Role,
                    content = m.Content
                }).ToList(),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                stream = true,
                user = request.UserId
            };

            var json = JsonSerializer.Serialize(openAiRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
        {
            var url = $"{BaseUrl}/embeddings";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var openAiRequest = new
            {
                model = request.ModelId,
                input = request.Inputs,
                user = request.UserId
            };

            var json = JsonSerializer.Serialize(openAiRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
        {
            var url = $"{BaseUrl}/moderations";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var openAiRequest = new
            {
                input = request.Input,
                model = request.ModelId
            };

            var json = JsonSerializer.Serialize(openAiRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
            HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);

            if (openAiResponse == null)
                throw new ProviderException($"Failed to parse OpenAI response: {content}", ProviderId);

            var choices = new List<ModelChoice>();
            foreach (var choice in openAiResponse.Choices)
            {
                var modelChoice = new ModelChoice
                {
                    Index = choice.Index,
                    Message = new ChatMessage
                    {
                        Role = choice.Message.Role,
                        Content = choice.Message.Content,
                        Name = choice.Message.Name,
                        FunctionCall = choice.Message.FunctionCall != null ? new FunctionCall
                        {
                            Name = choice.Message.FunctionCall.Name,
                            Arguments = choice.Message.FunctionCall.Arguments
                        } : null
                    },
                    FinishReason = choice.FinishReason
                };

                if (choice.Message.ToolCalls != null)
                {
                    modelChoice.Message.ToolCalls = choice.Message.ToolCalls.Select(t => new ToolCall
                    {
                        Id = t.Id,
                        Type = t.Type,
                        Function = new FunctionDefinition
                        {
                            Name = t.Function.Name,
                            Arguments = t.Function.Arguments
                        }
                    }).ToList();
                }

                choices.Add(modelChoice);
            }

            return new ModelResponse
            {
                Id = openAiResponse.Id,
                ModelUsed = openAiResponse.Model,
                Provider = ProviderName,
                Choices = choices,
                InputTokens = openAiResponse.Usage.PromptTokens,
                OutputTokens = openAiResponse.Usage.CompletionTokens,
                TotalTokens = openAiResponse.Usage.TotalTokens,
                Cost = await CalculateCostAsync(openAiResponse.Model, openAiResponse.Usage.PromptTokens, openAiResponse.Usage.CompletionTokens),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime,
                Created = openAiResponse.Created,
                SystemFingerprint = openAiResponse.SystemFingerprint,
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
            var openAiResponse = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(content, _jsonOptions);

            if (openAiResponse == null)
                throw new ProviderException($"Failed to parse OpenAI embedding response: {content}", ProviderId);

            var embeddings = openAiResponse.Data.Select((d, i) => new EmbeddingData
            {
                Index = i,
                Embedding = d.Embedding,
                Object = d.Object
            }).ToList();

            return new EmbeddingResponse
            {
                Model = openAiResponse.Model,
                Data = embeddings,
                InputTokens = openAiResponse.Usage.PromptTokens,
                Cost = await CalculateCostAsync(openAiResponse.Model, openAiResponse.Usage.PromptTokens, 0),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime,
                RequestId = Guid.NewGuid().ToString()
            };
        }

        protected override async Task<ModerationResponse> ParseModerationResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiModerationResponse>(content, _jsonOptions);

            if (openAiResponse == null)
                throw new ProviderException($"Failed to parse OpenAI moderation response: {content}", ProviderId);

            var results = openAiResponse.Results.Select(r => new ModerationResult
            {
                Categories = r.Categories,
                CategoryScores = r.CategoryScores,
                Flagged = r.Flagged
            }).ToList();

            return new ModerationResponse
            {
                Id = openAiResponse.Id,
                ModelUsed = openAiResponse.Model,
                Provider = ProviderName,
                IsFlagged = results.Any(r => r.Flagged),
                CategoryScores = results.SelectMany(r => r.CategoryScores)
                                        .GroupBy(kv => kv.Key)
                                        .ToDictionary(g => g.Key, g => g.Average(kv => kv.Value)),
                Categories = (Dictionary<string, ModerationCategory>)results.SelectMany(r => r.Categories),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime
            };
        }

        protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            var openAiResponse = JsonSerializer.Deserialize<OpenAiModelsResponse>(content, _jsonOptions);

            if (openAiResponse == null)
                throw new ProviderException($"Failed to parse OpenAI models response: {content}", ProviderId);

            var models = new List<ProviderModelInfo>();

            foreach (var model in openAiResponse.Data)
            {
                var modelInfo = new ProviderModelInfo
                {
                    ModelId = model.Id,
                    ModelName = model.Id,
                    DisplayName = FormatOpenAiModelName(model.Id),
                    Description = $"OpenAI {model.Id} model",
                    Provider = ProviderName,
                    ContextWindow = GetOpenAiContextWindow(model.Id),
                    MaxOutputTokens = GetOpenAiMaxOutputTokens(model.Id),
                    SupportsStreaming = model.Id.Contains("gpt"),
                    SupportsFunctionCalling = model.Id.Contains("gpt-4") || model.Id.Contains("gpt-3.5-turbo"),
                    SupportsVision = model.Id.Contains("vision"),
                    SupportsAudio = model.Id.Contains("whisper") || model.Id.Contains("tts"),
                    IsActive = !model.Id.Contains("deprecated"),
                    Pricing = new PricingInfo
                    {
                        InputTokenPrice = GetOpenAiInputTokenPrice(model.Id),
                        OutputTokenPrice = GetOpenAiOutputTokenPrice(model.Id),
                        PricingModel = "per-token"
                    },
                    Capabilities = GetOpenAiCapabilities(model.Id),
                    DeprecationDate = model.Id.Contains("deprecated") ? DateTime.UtcNow.AddDays(30) : default
                };

                models.Add(modelInfo);
            }

            return models;
        }

        protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
        {
            var content = await response.Content.ReadAsStringAsync();
            var model = JsonSerializer.Deserialize<OpenAiModel>(content, _jsonOptions);

            if (model == null)
                throw new ProviderException($"Failed to parse OpenAI model info response: {content}", ProviderId);

            return new ProviderModelInfo
            {
                ModelId = model.Id,
                ModelName = model.Id,
                DisplayName = FormatOpenAiModelName(model.Id),
                Description = $"OpenAI {model.Id} model",
                Provider = ProviderName,
                ContextWindow = GetOpenAiContextWindow(model.Id),
                MaxOutputTokens = GetOpenAiMaxOutputTokens(model.Id),
                SupportsStreaming = model.Id.Contains("gpt"),
                SupportsFunctionCalling = model.Id.Contains("gpt-4") || model.Id.Contains("gpt-3.5-turbo"),
                IsActive = !model.Id.Contains("deprecated"),
                Pricing = new PricingInfo
                {
                    InputTokenPrice = GetOpenAiInputTokenPrice(model.Id),
                    OutputTokenPrice = GetOpenAiOutputTokenPrice(model.Id),
                    PricingModel = "per-token"
                },
                Capabilities = GetOpenAiCapabilities(model.Id)
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
            // OpenAI doesn't have a dedicated health endpoint, so we use models endpoint
            var url = $"{BaseUrl}/models?limit=1";
            return new HttpRequestMessage(HttpMethod.Get, url);
        }

        protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("OpenAI-Beta", "assistants=v2");

            if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                request.Headers.Remove("Authorization");
                request.Headers.Add("Authorization", $"Bearer {_configuration.ApiKey}");
            }
        }

        #region OpenAI-specific Helper Methods

        private Dictionary<string, decimal> InitializeOpenAiPricing()
        {
            return new Dictionary<string, decimal>
            {
                ["gpt-4"] = 0.03m, // per 1K input tokens
                ["gpt-4-32k"] = 0.06m,
                ["gpt-4-turbo"] = 0.01m,
                ["gpt-4-vision"] = 0.01m,
                ["gpt-3.5-turbo"] = 0.0015m,
                ["gpt-3.5-turbo-16k"] = 0.003m,
                ["text-embedding-ada-002"] = 0.0001m,
                ["text-embedding-3-small"] = 0.00002m,
                ["text-embedding-3-large"] = 0.00013m,
                ["whisper-1"] = 0.006m, // per minute
                ["tts-1"] = 0.015m, // per 1K characters
                ["tts-1-hd"] = 0.030m
            };
        }

        private string FormatOpenAiModelName(string modelId)
        {
            return modelId.Replace("-", " ").ToUpperInvariant();
        }

        private int GetOpenAiContextWindow(string modelId)
        {
            if (modelId.Contains("32k")) return 32768;
            if (modelId.Contains("16k")) return 16384;
            if (modelId.Contains("gpt-4")) return 8192;
            if (modelId.Contains("gpt-3.5")) return 4096;
            if (modelId.Contains("text-embedding")) return 8191;
            return 2048;
        }

        private int GetOpenAiMaxOutputTokens(string modelId)
        {
            if (modelId.Contains("gpt-4-32k")) return 32768;
            if (modelId.Contains("gpt-4")) return 8192;
            if (modelId.Contains("gpt-3.5-turbo-16k")) return 16384;
            if (modelId.Contains("gpt-3.5-turbo")) return 4096;
            return 2048;
        }

        private decimal GetOpenAiInputTokenPrice(string modelId)
        {
            var basePrice = _openAiPricing.TryGetValue(modelId, out var price) ? price : 0.002m;
            return basePrice / 1000; // Convert from per 1K to per token
        }

        private decimal GetOpenAiOutputTokenPrice(string modelId)
        {
            var basePrice = _openAiPricing.TryGetValue(modelId, out var price) ? price : 0.002m;
            return modelId.Contains("gpt-4") ? (basePrice * 2) / 1000 : basePrice / 1000;
        }

        private List<ModelCapability> GetOpenAiCapabilities(string modelId)
        {
            var capabilities = new List<ModelCapability>();

            if (modelId.Contains("gpt-4") || modelId.Contains("gpt-3.5"))
            {
                capabilities.Add(new ModelCapability
                {
                    CapabilityType = CapabilityType.TextGeneration,
                    Score = modelId.Contains("gpt-4") ? 95 : 85
                });

                capabilities.Add(new ModelCapability
                {
                    CapabilityType = CapabilityType.CodeGeneration,
                    Score = modelId.Contains("gpt-4") ? 90 : 80
                });

                if (modelId.Contains("gpt-4"))
                {
                    capabilities.Add(new ModelCapability
                    {
                        CapabilityType = CapabilityType.FunctionCalling,
                        Score = 95
                    });
                }
            }

            if (modelId.Contains("vision"))
            {
                capabilities.Add(new ModelCapability
                {
                    CapabilityType = CapabilityType.Vision,
                    Score = 92
                });
            }

            if (modelId.Contains("whisper"))
            {
                capabilities.Add(new ModelCapability
                {
                    CapabilityType = CapabilityType.AudioTranscription,
                    Score = 95
                });
            }

            if (modelId.Contains("embedding"))
            {
                capabilities.Add(new ModelCapability
                {
                    CapabilityType = CapabilityType.TextEmbedding,
                    Score = 90
                });
            }

            return capabilities;
        }

        private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            var inputPrice = GetOpenAiInputTokenPrice(modelId);
            var outputPrice = GetOpenAiOutputTokenPrice(modelId);

            var inputCost = inputTokens * inputPrice;
            var outputCost = outputTokens * outputPrice;
            var totalCost = inputCost + outputCost;

            // Add service fee
            if (_configuration.ServiceFeePercentage > 0)
            {
                totalCost += totalCost * _configuration.ServiceFeePercentage;
            }

            return totalCost;
        }

        #endregion

    }
    #endregion
}

