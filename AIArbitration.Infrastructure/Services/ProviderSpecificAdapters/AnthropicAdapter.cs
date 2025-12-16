using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    // AnthropicAdapter.cs
    public class AnthropicAdapter : BaseProviderAdapter
    {
        private readonly AnthropicConfiguration _configuration;

        public override string ProviderId => "anthropic";
        public override string ProviderName => "Anthropic Claude";

        public AnthropicAdapter(
            HttpClient httpClient,
            ILogger<AnthropicAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            AnthropicConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"anthropic:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var anthropicRequest = MapToAnthropicRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/messages", anthropicRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var anthropicResponse = JsonSerializer.Deserialize<AnthropicMessageResponse>(content, _jsonOptions);

                return await ParseChatCompletionResponseAsync(anthropicResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        public override async Task<StreamingModelResponse> SendStreamingChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"anthropic:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var anthropicRequest = MapToAnthropicRequest(request);
                anthropicRequest.Stream = true;

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/messages", anthropicRequest);

                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();
                var streamingResponse = new StreamingModelResponse
                {
                    Stream = ParseStreamingResponse(stream),
                    ModelId = request.ModelId,
                    Provider = ProviderName,
                    ProcessingTime = TimeSpan.Zero,
                    RequestId = request.Id,
                    IsSuccess = true
                };

                return streamingResponse;
            }, "SendStreamingChatCompletionAsync");
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("x-api-key", _configuration.ApiKey);
            request.Headers.Add("anthropic-version", _configuration.ApiVersion);
            request.Headers.Add("anthropic-beta", "max-tokens-2024-07-15");
            request.Headers.UserAgent.ParseAdd("AIArbitrationEngine/1.0");
        }

        private AnthropicMessageRequest MapToAnthropicRequest(ChatRequest request)
        {
            // Anthropic uses a different message structure
            var messages = new List<AnthropicMessage>();
            var systemMessage = request.Messages.FirstOrDefault(m => m.Role == ChatRole.System);

            foreach (var msg in request.Messages)
            {
                if (msg.Role == ChatRole.System)
                    continue; // System message handled separately

                messages.Add(new AnthropicMessage
                {
                    Role = MapRole(msg.Role),
                    Content = new List<AnthropicContent>
                {
                    new AnthropicContent
                    {
                        Type = "text",
                        Text = msg.Content
                    }
                }
                });
            }

            return new AnthropicMessageRequest
            {
                Model = request.ModelId,
                Messages = messages,
                System = systemMessage?.Content,
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                TopP = request.TopP,
                Stream = false,
                StopSequences = request.StopSequences
            };
        }

        private string MapRole(ChatRole role)
        {
            return role switch
            {
                ChatRole.User => "user",
                ChatRole.Assistant => "assistant",
                ChatRole.System => "system",
                _ => "user"
            };
        }

        private async Task<ModelResponse> ParseChatCompletionResponseAsync(
            AnthropicMessageResponse response,
            TimeSpan processingTime,
            ChatRequest originalRequest)
        {
            var contentBlock = response.Content.FirstOrDefault(c => c.Type == "text");

            return new ModelResponse
            {
                Id = response.Id,
                ModelId = response.Model,
                Provider = ProviderName,
                Content = contentBlock?.Text,
                FinishReason = response.StopReason,
                InputTokens = response.Usage?.InputTokens ?? 0,
                OutputTokens = response.Usage?.OutputTokens ?? 0,
                TotalTokens = (response.Usage?.InputTokens ?? 0) + (response.Usage?.OutputTokens ?? 0),
                Cost = await EstimateCostAsync(
                    response.Usage?.InputTokens ?? 0,
                    response.Usage?.OutputTokens ?? 0),
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = contentBlock != null,
                ErrorMessage = contentBlock == null ? "No valid response from Anthropic" : null,
                Metadata = new Dictionary<string, object>
                {
                    ["anthropic_response_id"] = response.Id,
                    ["stop_reason"] = response.StopReason,
                    ["stop_sequence"] = response.StopSequence
                }
            };
        }

        private async IAsyncEnumerable<StreamingChunk> ParseStreamingResponse(Stream stream)
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
                    var chunk = JsonSerializer.Deserialize<AnthropicStreamingChunk>(data, _jsonOptions);

                    if (chunk?.Type == "content_block_delta" && chunk.Delta?.Text != null)
                    {
                        yield return new StreamingChunk
                        {
                            Content = chunk.Delta.Text,
                            FinishReason = null, // Will be in different chunk
                            Index = 0
                        };
                    }
                    else if (chunk?.Type == "message_stop")
                    {
                        yield return new StreamingChunk
                        {
                            Content = "",
                            FinishReason = "stop",
                            Index = 0
                        };
                    }
                }
                catch (JsonException)
                {
                    // Skip invalid JSON
                    continue;
                }
            }
        }

        public override async Task<CostEstimation> EstimateCostAsync(ChatRequest request)
        {
            // Anthropic pricing (Claude 3 Opus example)
            var inputCostPer1K = 0.015m; // $15 per 1M tokens = $0.015 per 1K
            var outputCostPer1K = 0.075m; // $75 per 1M tokens = $0.075 per 1K

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }

        // Anthropic doesn't have built-in moderation or embedding endpoints
        public override Task<ModerationResponse> SendModerationAsync(ModerationRequest request)
        {
            throw new NotSupportedException("Anthropic does not provide a moderation API");
        }

        public override Task<EmbeddingResponse> SendEmbeddingAsync(EmbeddingRequest request)
        {
            throw new NotSupportedException("Anthropic does not provide an embeddings API");
        }
    }

    // Anthropic DTOs
    public class AnthropicConfiguration
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.anthropic.com";
        public string ApiVersion { get; set; } = "2023-06-01";
        public int TimeoutSeconds { get; set; } = 30;
    }

    public class AnthropicMessageRequest
    {
        public string Model { get; set; }
        public List<AnthropicMessage> Messages { get; set; } = new List<AnthropicMessage>();
        public string System { get; set; }
        public int MaxTokens { get; set; } = 4096;
        public decimal? Temperature { get; set; }
        public decimal? TopP { get; set; }
        public decimal? TopK { get; set; }
        public bool Stream { get; set; }
        public List<string> StopSequences { get; set; } = new List<string>();
    }

    public class AnthropicMessage
    {
        public string Role { get; set; } // "user", "assistant"
        public List<AnthropicContent> Content { get; set; } = new List<AnthropicContent>();
    }

    public class AnthropicContent
    {
        public string Type { get; set; } // "text", "image"
        public string Text { get; set; }
    }

    public class AnthropicMessageResponse
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public string Role { get; set; }
        public List<AnthropicContent> Content { get; set; } = new List<AnthropicContent>();
        public string Model { get; set; }
        public string StopReason { get; set; }
        public string StopSequence { get; set; }
        public AnthropicUsage Usage { get; set; }
    }

    public class AnthropicUsage
    {
        public int InputTokens { get; set; }
        public int OutputTokens { get; set; }
    }

    public class AnthropicStreamingChunk
    {
        public string Type { get; set; }
        public AnthropicDelta Delta { get; set; }
        public string StopReason { get; set; }
        public string StopSequence { get; set; }
    }

    public class AnthropicDelta
    {
        public string Type { get; set; }
        public string Text { get; set; }
    }
}
