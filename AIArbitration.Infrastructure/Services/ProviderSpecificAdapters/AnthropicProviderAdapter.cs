using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    #region Anthropic Provider Adapter

    public class AnthropicProviderAdapter : BaseProviderAdapter
    {
        private readonly string _anthropicVersion = "2023-06-01";
        private readonly Dictionary<string, decimal> _anthropicPricing;

        public AnthropicProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<AnthropicProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider provider,
            ProviderConfiguration configuration)
            : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
        {
            _anthropicPricing = InitializeAnthropicPricing();
        }

        protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/messages";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var messages = new List<object>();
            foreach (var message in request.Messages)
            {
                if (message.Role == "system")
                {
                    // System messages are handled separately in Anthropic
                    continue;
                }

                messages.Add(new
                {
                    role = message.Role == "user" ? "user" : "assistant",
                    content = new[]
                    {
                    new
                    {
                        type = "text",
                        text = message.Content
                    }
                }
                });
            }

            var anthropicRequest = new
            {
                model = request.ModelId,
                max_tokens = request.MaxTokens ?? 1024,
                messages = messages,
                temperature = request.Temperature,
                system = request.Messages.FirstOrDefault(m => m.Role == "system")?.Content,
                stop_sequences = request.StopSequences,
                stream = false
            };

            var json = JsonSerializer.Serialize(anthropicRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/messages";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var anthropicRequest = new
            {
                model = request.ModelId,
                max_tokens = request.MaxTokens ?? 1024,
                messages = request.Messages.Select(m => new
                {
                    role = m.Role == "user" ? "user" : "assistant",
                    content = new[]
                    {
                    new
                    {
                        type = "text",
                        text = m.Content
                    }
                }
                }).ToList(),
                temperature = request.Temperature,
                stream = true
            };

            var json = JsonSerializer.Serialize(anthropicRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
        {
            throw new NotSupportedException("Anthropic does not support embeddings through their API");
        }

        protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
        {
            throw new NotSupportedException("Anthropic does not support moderation through their API");
        }

        protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
            HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var anthropicResponse = JsonSerializer.Deserialize<AnthropicChatResponse>(content, _jsonOptions);

            if (anthropicResponse == null)
                throw new ProviderException($"Failed to parse Anthropic response: {content}", ProviderId);

            var choices = new List<ModelChoice>
        {
            new ModelChoice
            {
                Index = 0,
                Message = new ChatMessage
                {
                    Role = "assistant",
                    Content = anthropicResponse.Content.FirstOrDefault()?.Text ?? string.Empty
                },
                FinishReason = anthropicResponse.StopReason
            }
        };

            return new ModelResponse
            {
                Id = anthropicResponse.Id,
                ModelUsed = anthropicResponse.Model,
                Provider = ProviderName,
                Choices = choices,
                InputTokens = anthropicResponse.Usage.InputTokens,
                OutputTokens = anthropicResponse.Usage.OutputTokens,
                TotalTokens = anthropicResponse.Usage.InputTokens + anthropicResponse.Usage.OutputTokens,
                Cost = await CalculateCostAsync(anthropicResponse.Model, anthropicResponse.Usage.InputTokens, anthropicResponse.Usage.OutputTokens),
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
            throw new NotSupportedException("Anthropic does not support embeddings through their API");
        }

        protected override async Task<ModerationResponse> ParseModerationResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime)
        {
            throw new NotSupportedException("Anthropic does not support moderation through their API");
        }

        protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
        {
            // Anthropic doesn't have a models endpoint, return static list
            return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "claude-3-opus-20240229",
                ModelName = "Claude 3 Opus",
                DisplayName = "Claude 3 Opus",
                Description = "Most powerful Claude model for highly complex tasks",
                Provider = ProviderName,
                ContextWindow = 200000,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000075m, // $0.075 per 1K tokens
                    OutputTokenPrice = 0.000375m, // $0.375 per 1K tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 96 },
                    new ModelCapability { CapabilityType = CapabilityType.Vision, Score = 94 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 92 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "claude-3-sonnet-20240229",
                ModelName = "Claude 3 Sonnet",
                DisplayName = "Claude 3 Sonnet",
                Description = "Balanced model for enterprise workloads",
                Provider = ProviderName,
                ContextWindow = 200000,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000003m, // $0.003 per 1K tokens
                    OutputTokenPrice = 0.000015m, // $0.015 per 1K tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 92 },
                    new ModelCapability { CapabilityType = CapabilityType.Vision, Score = 90 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 88 }
                }
            }
        };
        }

        protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
        {
            throw new NotSupportedException("Anthropic does not have a model info endpoint");
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
            // Use messages endpoint with minimal payload for health check
            var url = $"{BaseUrl}/messages";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var healthCheckRequest = new
            {
                model = "claude-3-sonnet-20240229",
                max_tokens = 1,
                messages = new[]
                {
                new
                {
                    role = "user",
                    content = new[]
                    {
                        new
                        {
                            type = "text",
                            text = "hello"
                        }
                    }
                }
            }
            };

            var json = JsonSerializer.Serialize(healthCheckRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("anthropic-version", _anthropicVersion);
            request.Headers.Add("x-api-key", _configuration.ApiKey);

            // Remove Authorization header since Anthropic uses x-api-key
            request.Headers.Remove("Authorization");
        }

        #region Anthropic-specific Helper Methods

        private Dictionary<string, decimal> InitializeAnthropicPricing()
        {
            return new Dictionary<string, decimal>
            {
                ["claude-3-opus-20240229"] = 0.075m, // per 1K input tokens
                ["claude-3-sonnet-20240229"] = 0.003m,
                ["claude-3-haiku-20240307"] = 0.00025m,
                ["claude-2.1"] = 0.008m
            };
        }

        private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            var basePrice = _anthropicPricing.TryGetValue(modelId, out var price) ? price : 0.008m;
            var inputCost = inputTokens * (basePrice / 1000);
            var outputCost = outputTokens * (basePrice * 5 / 1000); // Output is 5x input price for Anthropic
            var totalCost = inputCost + outputCost;

            // Add service fee
            if (_configuration.ServiceFeePercentage > 0)
            {
                totalCost += totalCost * _configuration.ServiceFeePercentage;
            }

            return totalCost;
        }

        #endregion

        #region Anthropic Response Classes

        private class AnthropicChatResponse
        {
            public string Id { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public List<AnthropicContent> Content { get; set; } = new();
            public string Model { get; set; } = string.Empty;
            public string StopReason { get; set; } = string.Empty;
            public string? StopSequence { get; set; }
            public AnthropicUsage Usage { get; set; } = new();
        }

        private class AnthropicContent
        {
            public string Type { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        private class AnthropicUsage
        {
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }

        #endregion
    }

    #endregion
    #region Cohere Provider Adapter
#endregion
}
