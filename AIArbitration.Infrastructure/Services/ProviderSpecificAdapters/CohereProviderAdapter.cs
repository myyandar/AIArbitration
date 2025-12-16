using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Entities.Enums;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    #region Cohere Provider Adapter

    public class CohereProviderAdapter : BaseProviderAdapter
    {
        private readonly Dictionary<string, decimal> _coherePricing;

        public CohereProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<CohereProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider provider,
            ProviderConfiguration configuration)
            : base(dbContext, httpClientFactory, logger, circuitBreaker, rateLimiter, cache, provider, configuration)
        {
            _coherePricing = InitializeCoherePricing();
        }

        protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/chat";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var cohereRequest = new
            {
                model = request.ModelId,
                message = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty,
                chat_history = request.Messages.Take(request.Messages.Count - 1).Select(m => new
                {
                    role = m.Role == "user" ? "USER" : "CHATBOT",
                    message = m.Content
                }).ToList(),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                p = request.TopP,
                frequency_penalty = request.FrequencyPenalty,
                presence_penalty = request.PresencePenalty,
                stop_sequences = request.StopSequences,
                stream = false,
                connectors = request.Tools?.Any() == true ? new[] { new { id = "web-search" } } : null
            };

            var json = JsonSerializer.Serialize(cohereRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
        {
            var url = $"{BaseUrl}/chat";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var cohereRequest = new
            {
                model = request.ModelId,
                message = request.Messages.LastOrDefault(m => m.Role == "user")?.Content ?? string.Empty,
                chat_history = request.Messages.Take(request.Messages.Count - 1).Select(m => new
                {
                    role = m.Role == "user" ? "USER" : "CHATBOT",
                    message = m.Content
                }).ToList(),
                temperature = request.Temperature,
                max_tokens = request.MaxTokens,
                stream = true
            };

            var json = JsonSerializer.Serialize(cohereRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
        {
            var url = $"{BaseUrl}/embed";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var cohereRequest = new
            {
                model = request.ModelId,
                texts = request.Inputs,
                input_type = "search_document",
                truncate = "END"
            };

            var json = JsonSerializer.Serialize(cohereRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
        {
            var url = $"{BaseUrl}/classify";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var cohereRequest = new
            {
                model = request.ModelId,
                inputs = new[] { request.Input },
                truncate = "END"
            };

            var json = JsonSerializer.Serialize(cohereRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
            HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var cohereResponse = JsonSerializer.Deserialize<CohereChatResponse>(content, _jsonOptions);

            if (cohereResponse == null)
                throw new ProviderException($"Failed to parse Cohere response: {content}", ProviderId);

            var choices = new List<ModelChoice>
        {
            new ModelChoice
            {
                Index = 0,
                Message = new ChatMessage
                {
                    Role = "assistant",
                    Content = cohereResponse.Text
                },
                FinishReason = cohereResponse.FinishReason
            }
        };

            return new ModelResponse
            {
                Id = cohereResponse.Id ?? Guid.NewGuid().ToString(),
                ModelUsed = cohereResponse.Model,
                Provider = ProviderName,
                Choices = choices,
                InputTokens = cohereResponse.Meta?.Tokens?.InputTokens ?? 0,
                OutputTokens = cohereResponse.Meta?.Tokens?.OutputTokens ?? 0,
                TotalTokens = (cohereResponse.Meta?.Tokens?.InputTokens ?? 0) + (cohereResponse.Meta?.Tokens?.OutputTokens ?? 0),
                Cost = await CalculateCostAsync(cohereResponse.Model, cohereResponse.Meta?.Tokens?.InputTokens ?? 0, cohereResponse.Meta?.Tokens?.OutputTokens ?? 0),
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
            var cohereResponse = JsonSerializer.Deserialize<CohereEmbeddingResponse>(content, _jsonOptions);

            if (cohereResponse == null)
                throw new ProviderException($"Failed to parse Cohere embedding response: {content}", ProviderId);

            var embeddings = cohereResponse.Embeddings.Select((e, i) => new EmbeddingData
            {
                Index = i,
                Embedding = e
            }).ToList();

            return new EmbeddingResponse
            {
                Model = cohereResponse.Model,
                Data = embeddings,
                InputTokens = EstimateTokenCount(cohereResponse.Embeddings),
                Cost = await CalculateCostAsync(cohereResponse.Model, EstimateTokenCount(cohereResponse.Embeddings), 0),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime,
                RequestId = Guid.NewGuid().ToString()
            };
        }

        protected override async Task<ModerationResponse> ParseModerationResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var cohereResponse = JsonSerializer.Deserialize<CohereClassifyResponse>(content, _jsonOptions);

            if (cohereResponse == null)
                throw new ProviderException($"Failed to parse Cohere moderation response: {content}", ProviderId);
            
            var results = cohereResponse.Results.Select(r => new ModerationResult
            {
                Categories = r.Labels.ToDictionary(
                    l => l,
                    l => new ModerationCategory
                    {
                        Flagged = true,
                        Score = 1.0,
                        Description = string.Empty
                    }
                ),
                CategoryScores = r.Confidence.ToDictionary(l => l.Label, l => l.Confidence),
                Flagged = r.Prediction == "toxic" || r.Prediction == "harmful"
            }).ToList();

            return new ModerationResponse
            {
                ModelUsed = cohereResponse.Model,
                Provider = ProviderName,
                IsFlagged = results.Any(r => r.Flagged),
                CategoryScores = results.SelectMany(r => r.CategoryScores).ToDictionary(kv => kv.Key, kv => kv.Value),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime
            };
        }

        protected override async Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
        {
            // Cohere doesn't have a models endpoint, return static list
            return new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "command-r",
                ModelName = "Command R",
                DisplayName = "Command R",
                Description = "Balanced model for RAG and tool use",
                Provider = ProviderName,
                ContextWindow = 128000,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000005m, // $0.50 per 1M tokens
                    OutputTokenPrice = 0.0000015m, // $1.50 per 1M tokens
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 89 },
                    new ModelCapability { CapabilityType = CapabilityType.CodeGeneration, Score = 82 },
                    new ModelCapability { CapabilityType = CapabilityType.FunctionCalling, Score = 85 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "embed-english-v3.0",
                ModelName = "Embed English V3",
                DisplayName = "Embed English V3",
                Description = "English text embedding model",
                Provider = ProviderName,
                ContextWindow = 512,
                MaxOutputTokens = 0,
                SupportsStreaming = false,
                SupportsFunctionCalling = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000001m, // $0.10 per 1M tokens
                    OutputTokenPrice = 0m,
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextEmbedding, Score = 90 }
                }
            }
        };
        }

        protected override async Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
        {
            throw new NotSupportedException("Cohere does not have a model info endpoint");
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
            var url = $"{BaseUrl}/generate";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var healthCheckRequest = new
            {
                model = "command",
                prompt = "Hello",
                max_tokens = 1
            };

            var json = JsonSerializer.Serialize(healthCheckRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            return httpRequest;
        }

        protected override void AddProviderSpecificHeaders(HttpRequestMessage request)
        {
            // Cohere uses standard Authorization header
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        #region Cohere-specific Helper Methods

        private Dictionary<string, decimal> InitializeCoherePricing()
        {
            return new Dictionary<string, decimal>
            {
                ["command-r"] = 0.5m, // per 1M input tokens
                ["command-r-plus"] = 3.0m,
                ["command"] = 1.5m,
                ["embed-english-v3.0"] = 0.1m,
                ["embed-multilingual-v3.0"] = 0.1m
            };
        }

        private int EstimateTokenCount(List<List<float>> embeddings)
        {
            // Rough estimate: assume 768 dimensions per embedding
            return embeddings.Count * 768;
        }

        private async Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            var basePrice = _coherePricing.TryGetValue(modelId, out var price) ? price : 1.0m;
            var inputCost = inputTokens * (basePrice / 1000000m);
            var outputCost = outputTokens * (basePrice * 2 / 1000000m); // Output is typically 2x input price
            var totalCost = inputCost + outputCost;

            // Add service fee
            if (_configuration.ServiceFeePercentage > 0)
            {
                totalCost += totalCost * _configuration.ServiceFeePercentage;
            }

            return totalCost;
        }

        #endregion

        #region Cohere Response Classes

        private class CohereChatResponse
        {
            public string? Id { get; set; }
            public string Text { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string FinishReason { get; set; } = string.Empty;
            public CohereMeta? Meta { get; set; }
        }

        private class CohereMeta
        {
            public CohereTokens? Tokens { get; set; }
        }

        private class CohereTokens
        {
            public int InputTokens { get; set; }
            public int OutputTokens { get; set; }
        }

        private class CohereEmbeddingResponse
        {
            public string Model { get; set; } = string.Empty;
            public List<List<float>> Embeddings { get; set; } = new();
        }

        private class CohereClassifyResponse
        {
            public string Model { get; set; } = string.Empty;
            public List<CohereClassificationResult> Results { get; set; } = new();
        }

        private class CohereClassificationResult
        {
            public List<string> Labels { get; set; } = new();
            public List<CohereConfidence> Confidence { get; set; } = new();
            public string Prediction { get; set; } = string.Empty;
        }

        private class CohereConfidence
        {
            public string Label { get; set; } = string.Empty;
            public decimal Confidence { get; set; }
        }

        #endregion
    }
    #endregion

    public class CohereAdapter : BaseProviderAdapter
    {
        private readonly CohereConfiguration _configuration;

        public override string ProviderId => "cohere";
        public override string ProviderName => "Cohere";

        public CohereAdapter(
            HttpClient httpClient,
            ILogger<CohereAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            CohereConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"cohere:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var cohereRequest = MapToCohereRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat", cohereRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var cohereResponse = JsonSerializer.Deserialize<CohereChatResponse>(content, _jsonOptions);

                return await ParseChatCompletionResponseAsync(cohereResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        public override async Task<StreamingModelResponse> SendStreamingChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"cohere:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var cohereRequest = MapToCohereRequest(request);
                cohereRequest.Stream = true;

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat", cohereRequest);

                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();

                return new StreamingModelResponse
                {
                    Stream = ParseCohereStreamingResponse(stream),
                    ModelId = request.ModelId,
                    Provider = ProviderName,
                    ProcessingTime = TimeSpan.Zero,
                    RequestId = request.Id,
                    IsSuccess = true
                };
            }, "SendStreamingChatCompletionAsync");
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("Authorization", $"Bearer {_configuration.ApiKey}");
            request.Headers.Add("Request-Source", "AIArbitrationEngine");
            request.Headers.Add("Cohere-Version", "2022-12-06");
        }

        private CohereChatRequest MapToCohereRequest(ChatRequest request)
        {
            // Convert chat messages to Cohere format
            var userMessages = request.Messages.Where(m => m.Role == ChatRole.User).ToList();
            var assistantMessages = request.Messages.Where(m => m.Role == ChatRole.Assistant).ToList();

            var chatHistory = new List<CohereChatHistory>();

            // Pair user and assistant messages
            for (int i = 0; i < Math.Min(userMessages.Count, assistantMessages.Count); i++)
            {
                chatHistory.Add(new CohereChatHistory
                {
                    Role = "USER",
                    Message = userMessages[i].Content
                });

                chatHistory.Add(new CohereChatHistory
                {
                    Role = "CHATBOT",
                    Message = assistantMessages[i].Content
                });
            }

            // Last user message is the current message
            var currentMessage = userMessages.LastOrDefault()?.Content ??
                               request.Messages.LastOrDefault(m => m.Role != ChatRole.System)?.Content;

            return new CohereChatRequest
            {
                Model = request.ModelId,
                Message = currentMessage,
                ChatHistory = chatHistory,
                PromptTruncation = "AUTO",
                Temperature = (float?)request.Temperature,
                MaxTokens = (int?)request.MaxTokens,
                Stream = false
            };
        }

        private async Task<ModelResponse> ParseChatCompletionResponseAsync(
            CohereChatResponse response,
            TimeSpan processingTime,
            ChatRequest originalRequest)
        {
            return new ModelResponse
            {
                Id = response.Id ?? Guid.NewGuid().ToString(),
                ModelId = response.Model,
                Provider = ProviderName,
                Content = response.Text,
                FinishReason = response.FinishReason,
                InputTokens = response.Meta?.Tokens?.InputTokens ?? 0,
                OutputTokens = response.Meta?.Tokens?.OutputTokens ?? 0,
                TotalTokens = (response.Meta?.Tokens?.InputTokens ?? 0) +
                             (response.Meta?.Tokens?.OutputTokens ?? 0),
                Cost = await EstimateCostAsync(
                    response.Meta?.Tokens?.InputTokens ?? 0,
                    response.Meta?.Tokens?.OutputTokens ?? 0),
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = !string.IsNullOrEmpty(response.Text),
                ErrorMessage = string.IsNullOrEmpty(response.Text) ? "No response from Cohere" : null,
                Metadata = new Dictionary<string, object>
                {
                    ["cohere_response_id"] = response.Id,
                    ["finish_reason"] = response.FinishReason,
                    ["generation_id"] = response.GenerationId
                }
            };
        }

        private async IAsyncEnumerable<StreamingChunk> ParseCohereStreamingResponse(Stream stream)
        {
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (string.IsNullOrEmpty(line) || !line.StartsWith("data:"))
                    continue;

                var data = line.Substring(5).Trim();
                if (data == "[DONE]")
                    yield break;

                try
                {
                    var chunk = JsonSerializer.Deserialize<CohereStreamingChunk>(data, _jsonOptions);
                    if (chunk?.Text != null)
                    {
                        yield return new StreamingChunk
                        {
                            Content = chunk.Text,
                            FinishReason = chunk.IsFinished ? "COMPLETE" : null
                        };
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Cohere pricing varies by model
            var model = _configuration.DefaultModel ?? "command-r-plus";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "command-r-plus" => (0.0015m, 0.006m),      // $1.5/$6 per 1M tokens
                "command-r" => (0.0005m, 0.002m),          // $0.5/$2 per 1M tokens
                "command" => (0.0005m, 0.0015m),          // $0.5/$1.5 per 1M tokens
                "embed-english-v3.0" => (0.0001m, 0m),    // $0.1 per 1M tokens (embedding)
                _ => (0.001m, 0.003m)                     // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }

        public override async Task<EmbeddingResponse> SendEmbeddingAsync(EmbeddingRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var cohereRequest = new CohereEmbeddingRequest
                {
                    Texts = new List<string> { request.Input },
                    Model = request.ModelId ?? "embed-english-v3.0",
                    InputType = "search_document",
                    Truncate = "END"
                };

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/embed", cohereRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var cohereResponse = JsonSerializer.Deserialize<CohereEmbeddingResponse>(content, _jsonOptions);

                return new EmbeddingResponse
                {
                    Id = cohereResponse.Id ?? Guid.NewGuid().ToString(),
                    ModelId = cohereResponse.Model,
                    Provider = ProviderName,
                    Embeddings = cohereResponse.Embeddings,
                    InputTokens = 0, // Cohere embeddings don't return token counts
                    TotalTokens = 0,
                    Cost = await EstimateCostAsync(0, 0), // Will need special handling for embeddings
                    ProcessingTime = stopwatch.Elapsed,
                    Timestamp = DateTime.UtcNow,
                    Success = cohereResponse.Embeddings?.Any() == true
                };
            }, "SendEmbeddingAsync");
        }

        public override Task<ModerationResponse> SendModerationAsync(ModerationRequest request)
        {
            throw new NotSupportedException("Cohere does not provide a moderation API");
        }

        public override Task<ModelCapabilities> GetCapabilitiesAsync()
        {
            return Task.FromResult(new ModelCapabilities
            {
                ProviderId = ProviderId,
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = false,
                SupportsVision = false,
                SupportsAudio = false,
                MaxContextLength = 4096,
                AvailableModels = new List<string>
                {
                    "command",
                    "command-r",
                    "command-r-plus",
                    "embed-english-v3.0",
                    "embed-multilingual-v3.0"
                },
                UpdatedAt = DateTime.UtcNow
            });
        }
    }
}

