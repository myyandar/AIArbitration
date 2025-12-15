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

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    public class AmazonBedrockProviderAdapter : BaseProviderAdapter
    {
        private readonly string _awsRegion;
        private readonly Amazon.Runtime.AWSCredentials _awsCredentials;
        private readonly Dictionary<string, decimal> _bedrockPricing;

        public AmazonBedrockProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<AmazonBedrockProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider provider,
            ProviderConfiguration configuration)
            : base(dbContext, httpClientFactory,
                  logger, // This is acceptable as ILogger<BaseProviderAdapter> due to covariance
                  circuitBreaker, rateLimiter, cache, provider, configuration)
        {
            _awsRegion = configuration.ApiSecret ?? "us-east-1";
            _awsCredentials = new Amazon.Runtime.BasicAWSCredentials(
                configuration.ApiKey,
                configuration.ApiSecret);
            _bedrockPricing = InitializeBedrockPricing();
        }

        protected override async Task<HttpRequestMessage> CreateChatCompletionRequestAsync(AIArbitration.Core.Entities.ChatRequest request)
        {
            var modelId = request.ModelId;
            var url = $"https://bedrock-runtime.{_awsRegion}.amazonaws.com/model/{modelId}/invoke";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            object bedrockRequest;
            if (modelId.Contains("anthropic"))
            {
                var messages = request.Messages.Select(m => new
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
                }).ToList();

                bedrockRequest = new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = request.MaxTokens ?? 1024,
                    messages = messages,
                    temperature = request.Temperature,
                    top_p = request.TopP,
                    stop_sequences = request.StopSequences
                };
            }
            else if (modelId.Contains("cohere"))
            {
                bedrockRequest = new
                {
                    max_tokens = request.MaxTokens ?? 1024,
                    temperature = request.Temperature,
                    p = request.TopP,
                    k = 0,
                    stop_sequences = request.StopSequences,
                    stream = false,
                    prompt = FormatCoherePrompt(request.Messages)
                };
            }
            else if (modelId.Contains("meta"))
            {
                bedrockRequest = new
                {
                    prompt = FormatLlamaPrompt(request.Messages),
                    max_gen_len = request.MaxTokens ?? 512,
                    temperature = request.Temperature,
                    top_p = request.TopP
                };
            }
            else
            {
                bedrockRequest = new
                {
                    max_tokens = request.MaxTokens ?? 1024,
                    temperature = request.Temperature,
                    prompt = FormatPrompt(request.Messages)
                };
            }

            var json = JsonSerializer.Serialize(bedrockRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await SignAwsRequestAsync(httpRequest);
            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateStreamingChatCompletionRequestAsync(ChatRequest request)
        {
            var modelId = request.ModelId;
            var url = $"https://bedrock-runtime.{_awsRegion}.amazonaws.com/model/{modelId}/invoke-with-response-stream";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            object bedrockRequest;
            if (modelId.Contains("anthropic"))
            {
                bedrockRequest = new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = request.MaxTokens ?? 1024,
                    messages = request.Messages.Select(m => new
                    {
                        role = m.Role == "user" ? "user" : "assistant",
                        content = new[]
                        {
                        new { type = "text", text = m.Content }
                    }
                    }).ToList(),
                    temperature = request.Temperature,
                    top_p = request.TopP,
                    stop_sequences = request.StopSequences
                };
            }
            else
            {
                bedrockRequest = new
                {
                    max_tokens = request.MaxTokens ?? 1024,
                    temperature = request.Temperature,
                    prompt = FormatPrompt(request.Messages)
                };
            }

            var json = JsonSerializer.Serialize(bedrockRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await SignAwsRequestAsync(httpRequest);
            return httpRequest;
        }

        protected override async Task<HttpRequestMessage> CreateEmbeddingRequestAsync(EmbeddingRequest request)
        {
            var url = $"https://bedrock-runtime.{_awsRegion}.amazonaws.com/model/{request.ModelId}/invoke";
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);

            var bedrockRequest = new
            {
                inputText = string.Join(" ", request.Inputs)
            };

            var json = JsonSerializer.Serialize(bedrockRequest, _jsonOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            await SignAwsRequestAsync(httpRequest);
            return httpRequest;
        }

        protected override Task<HttpRequestMessage> CreateModerationRequestAsync(ModerationRequest request)
        {
            throw new NotSupportedException("Amazon Bedrock does not have a dedicated moderation endpoint");
        }

        protected override async Task<ModelResponse> ParseChatCompletionResponseAsync(
            HttpResponseMessage response, ChatRequest originalRequest, TimeSpan processingTime)
        {
            var content = await response.Content.ReadAsStringAsync();
            var bedrockResponse = JsonSerializer.Deserialize<BedrockResponse>(content, _jsonOptions);

            if (bedrockResponse == null)
                throw new ProviderException($"Failed to parse Bedrock response: {content}", ProviderId);

            var responseContent = ExtractContent(bedrockResponse, originalRequest.ModelId);
            var finishReason = ExtractFinishReason(bedrockResponse, originalRequest.ModelId);

            var choices = new List<ModelChoice>
        {
            new ModelChoice
            {
                Index = 0,
                Message = new ChatMessage
                {
                    Role = "assistant",
                    Content = responseContent
                },
                FinishReason = finishReason
            }
        };

            var inputTokens = EstimateTokenCount(originalRequest.Messages.Sum(m => m.Content?.Length ?? 0));
            var outputTokens = EstimateTokenCount(responseContent.Length);

            return new ModelResponse
            {
                // Id = Guid.NewGuid().ToString(),
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
            var bedrockResponse = JsonSerializer.Deserialize<BedrockEmbeddingResponse>(content, _jsonOptions);

            if (bedrockResponse == null)
                throw new ProviderException($"Failed to parse Bedrock embedding response: {content}", ProviderId);

            var embeddings = new List<EmbeddingData>
        {
            new EmbeddingData
            {
                Index = 0,
                Embedding = bedrockResponse.Embedding ?? new List<float>()
            }
        };

            var inputTokens = EstimateTokenCount(bedrockResponse.Embedding?.Count ?? 0 * 4);

            return new EmbeddingResponse
            {
                Model = "amazon.titan-embed-text-v1",
                Data = embeddings,
                InputTokens = inputTokens,
                Cost = await CalculateCostAsync("amazon.titan-embed-text-v1", inputTokens, 0),
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime,
                RequestId = Guid.NewGuid().ToString()
            };
        }

        protected override Task<ModerationResponse> ParseModerationResponseAsync(
            HttpResponseMessage response, TimeSpan processingTime)
        {
            throw new NotSupportedException("Amazon Bedrock does not have a dedicated moderation endpoint");
        }

        protected override Task<List<ProviderModelInfo>> ParseAvailableModelsResponseAsync(HttpResponseMessage response)
        {
            // Since Bedrock doesn't have a models endpoint, return hardcoded list
            // This method still needs to satisfy the abstract signature
            var models = new List<ProviderModelInfo>
        {
            new ProviderModelInfo
            {
                ModelId = "anthropic.claude-3-sonnet-20240229-v1:0",
                ModelName = "Claude 3 Sonnet",
                DisplayName = "Claude 3 Sonnet (Bedrock)",
                Description = "Claude 3 Sonnet model on Amazon Bedrock",
                Provider = ProviderName,
                ContextWindow = 200000,
                MaxOutputTokens = 4096,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = true,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.000003m,
                    OutputTokenPrice = 0.000015m,
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextGeneration, Score = 92 },
                    new ModelCapability { CapabilityType = CapabilityType.Vision, Score = 90 }
                }
            },
            new ProviderModelInfo
            {
                ModelId = "meta.llama3-70b-instruct-v1:0",
                ModelName = "Llama 3 70B",
                DisplayName = "Llama 3 70B (Bedrock)",
                Description = "Meta's Llama 3 70B model on Amazon Bedrock",
                Provider = ProviderName,
                ContextWindow = 8192,
                MaxOutputTokens = 2048,
                SupportsStreaming = true,
                SupportsFunctionCalling = false,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.00000195m,
                    OutputTokenPrice = 0.00000256m,
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
                ModelId = "amazon.titan-embed-text-v1",
                ModelName = "Titan Embed Text",
                DisplayName = "Titan Embed Text",
                Description = "Amazon's embedding model for text",
                Provider = ProviderName,
                ContextWindow = 8192,
                MaxOutputTokens = 0,
                SupportsStreaming = false,
                SupportsFunctionCalling = false,
                SupportsVision = false,
                IsActive = true,
                Pricing = new PricingInfo
                {
                    InputTokenPrice = 0.0000001m,
                    OutputTokenPrice = 0m,
                    PricingModel = "per-token"
                },
                Capabilities = new List<ModelCapability>
                {
                    new ModelCapability { CapabilityType = CapabilityType.TextEmbedding, Score = 85 }
                }
            }
        };

            return Task.FromResult(models);
        }

        protected override Task<ProviderModelInfo> ParseModelInfoResponseAsync(HttpResponseMessage response)
        {
            return Task.FromException<ProviderModelInfo>(
                new NotSupportedException("Amazon Bedrock does not have a model info endpoint"));
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
            var url = $"https://bedrock.{_awsRegion}.amazonaws.com/foundation-models";
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            await SignAwsRequestAsync(httpRequest);
            return httpRequest;
        }

        #region Bedrock-specific Helper Methods

        private Dictionary<string, decimal> InitializeBedrockPricing()
        {
            return new Dictionary<string, decimal>
            {
                ["anthropic.claude-3-sonnet-20240229-v1:0"] = 3.0m,
                ["anthropic.claude-3-haiku-20240307-v1:0"] = 0.25m,
                ["meta.llama3-70b-instruct-v1:0"] = 1.95m,
                ["meta.llama3-8b-instruct-v1:0"] = 0.60m,
                ["amazon.titan-embed-text-v1"] = 0.10m,
                ["cohere.command-text-v14"] = 1.50m
            };
        }

        private async Task SignAwsRequestAsync(HttpRequestMessage request)
        {
            // In a real implementation, you would use AWS SDK to sign the request
            var date = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
            request.Headers.Add("X-Amz-Date", date);
            request.Headers.Add("X-Amz-Target", "BedrockRuntime.InvokeModel");

            // Add authorization header placeholder
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "AWS4-HMAC-SHA256",
                $"Credential={_awsCredentials.GetCredentials().AccessKey}/{DateTime.UtcNow:yyyyMMdd}/{_awsRegion}/bedrock/aws4_request");

            await Task.CompletedTask; // For async signature
        }

        private string FormatPrompt(List<ChatMessage> messages)
        {
            var prompt = new StringBuilder();
            foreach (var message in messages)
            {
                prompt.AppendLine($"{message.Role}: {message.Content}");
            }
            prompt.AppendLine("assistant:");
            return prompt.ToString();
        }

        private string FormatCoherePrompt(List<ChatMessage> messages)
        {
            var prompt = new StringBuilder();
            foreach (var message in messages)
            {
                var role = message.Role == "user" ? "Human" : "Assistant";
                prompt.AppendLine($"{role}: {message.Content}");
            }
            prompt.AppendLine("Assistant:");
            return prompt.ToString();
        }

        private string FormatLlamaPrompt(List<ChatMessage> messages)
        {
            var prompt = new StringBuilder();
            prompt.AppendLine("<s>[INST] <<SYS>>");
            prompt.AppendLine("You are a helpful AI assistant.");
            prompt.AppendLine("<</SYS>>");

            foreach (var message in messages)
            {
                if (message.Role == "user")
                {
                    prompt.AppendLine($"{message.Content}[/INST]");
                }
                else
                {
                    prompt.AppendLine($"{message.Content}</s><s>[INST]");
                }
            }

            return prompt.ToString();
        }

        private string ExtractContent(BedrockResponse response, string modelId)
        {
            if (response == null) return string.Empty;

            if (modelId.Contains("anthropic"))
            {
                return response.Content?.FirstOrDefault()?.Text ?? string.Empty;
            }
            else if (modelId.Contains("cohere"))
            {
                return response.Generations?.FirstOrDefault()?.Text ?? string.Empty;
            }
            else if (modelId.Contains("meta"))
            {
                return response.Generation ?? string.Empty;
            }

            return response.Completion ?? string.Empty;
        }

        private string ExtractFinishReason(BedrockResponse response, string modelId)
        {
            if (modelId.Contains("anthropic"))
            {
                return response.StopReason ?? "stop";
            }

            return response.StopSequence != null ? "stop" : "length";
        }

        private int EstimateTokenCount(int characterCount)
        {
            return (int)Math.Ceiling(characterCount / 4.0);
        }

        private Task<decimal> CalculateCostAsync(string modelId, int inputTokens, int outputTokens)
        {
            var basePrice = _bedrockPricing.TryGetValue(modelId, out var price) ? price : 1.0m;
            var inputCost = inputTokens * (basePrice / 1000000m);
            var outputCost = outputTokens * ((basePrice * (modelId.Contains("anthropic") ? 5 : 1.3m)) / 1000000m);
            var totalCost = inputCost + outputCost;

            if (_configuration.ServiceFeePercentage > 0)
            {
                totalCost += totalCost * (_configuration.ServiceFeePercentage / 100m);
            }

            return Task.FromResult(totalCost);
        }

        #endregion

        #region Bedrock Response Classes

        private class BedrockResponse
        {
            public List<BedrockContent>? Content { get; set; }
            public string? StopReason { get; set; }
            public string? StopSequence { get; set; }
            public string? Completion { get; set; }
            public List<BedrockGeneration>? Generations { get; set; }
            public string? Generation { get; set; }
        }

        private class BedrockContent
        {
            public string Type { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        private class BedrockGeneration
        {
            public string Text { get; set; } = string.Empty;
        }

        private class BedrockEmbeddingResponse
        {
            public List<float>? Embedding { get; set; }
            public string? InputTextTokenCount { get; set; }
        }

        #endregion
    }
}
