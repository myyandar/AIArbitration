using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    // OpenAiAdapter.cs
    public class OpenAiAdapter : BaseProviderAdapter
    {
        private readonly OpenAiConfiguration _configuration;

        public override string ProviderId => "openai";
        public override string ProviderName => "OpenAI";

        public OpenAiAdapter(
            HttpClient httpClient,
            ILogger<OpenAiAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            OpenAiConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"openai:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var openAiRequest = MapToOpenAiRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat/completions", openAiRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);

                return await ParseChatCompletionResponseAsync(openAiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        public override async Task<StreamingModelResponse> SendStreamingChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimitAsync($"openai:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var openAiRequest = MapToOpenAiRequest(request);
                openAiRequest.Stream = true;

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat/completions", openAiRequest);

                var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var stream = await response.Content.ReadAsStreamAsync();
                var streamingResponse = new StreamingModelResponse
                {
                    Stream = ParseStreamingResponse(stream),
                    ModelId = request.ModelId,
                    Provider = ProviderName,
                    ProcessingTime = TimeSpan.Zero, // Will be calculated on completion
                    RequestId = request.Id,
                    IsSuccess = true
                };

                return streamingResponse;
            }, "SendStreamingChatCompletionAsync");
        }

        public override async Task<ModerationResponse> SendModerationAsync(ModerationRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var openAiRequest = new OpenAiModerationRequest
                {
                    Input = request.Content,
                    Model = "text-moderation-latest"
                };

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/moderations", openAiRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiModerationResponse>(content, _jsonOptions);

                return ParseModerationResponse(openAiResponse, stopwatch.Elapsed, request);
            }, "SendModerationAsync");
        }

        public override async Task<EmbeddingResponse> SendEmbeddingAsync(EmbeddingRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var openAiRequest = new OpenAiEmbeddingRequest
                {
                    Input = request.Input,
                    Model = request.ModelId ?? "text-embedding-ada-002",
                    EncodingFormat = "float"
                };

                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/embeddings", openAiRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(content, _jsonOptions);

                return ParseEmbeddingResponse(openAiResponse, stopwatch.Elapsed, request);
            }, "SendEmbeddingAsync");
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Example costs per 1K tokens (adjust based on actual model)
            var inputCostPer1K = 0.0015m; // $0.0015 per 1K input tokens
            var outputCostPer1K = 0.0020m; // $0.0020 per 1K output tokens

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
            request.Headers.Add("OpenAI-Organization", _configuration.OrganizationId);
            request.Headers.UserAgent.ParseAdd("AIArbitrationEngine/1.0");
        }

        // Helper methods
        private OpenAiChatRequest MapToOpenAiRequest(ChatRequest request)
        {
            return new OpenAiChatRequest
            {
                Model = request.ModelId,
                Messages = request.Messages.Select(m => new OpenAiMessage
                {
                    Role = m.Role.ToString().ToLower(),
                    Content = m.Content,
                    Name = m.Name
                }).ToList(),
                MaxTokens = request.MaxTokens,
                Temperature = request.Temperature,
                TopP = request.TopP,
                FrequencyPenalty = request.FrequencyPenalty,
                PresencePenalty = request.PresencePenalty,
                Stream = false,
                Stop = request.StopSequences,
                User = request.UserId
            };
        }

        private async Task<ModelResponse> ParseChatCompletionResponseAsync(
            OpenAiChatCompletionResponse response,
            TimeSpan processingTime,
            ChatRequest originalRequest)
        {
            var choice = response.Choices.FirstOrDefault();

            return new ModelResponse
            {
                Id = response.Id,
                ModelId = response.Model,
                Provider = ProviderName,
                Content = choice?.Message?.Content,
                FinishReason = choice?.FinishReason,
                InputTokens = response.Usage?.PromptTokens ?? 0,
                OutputTokens = response.Usage?.CompletionTokens ?? 0,
                TotalTokens = response.Usage?.TotalTokens ?? 0,
                Cost = await EstimateCostAsync(
                    response.Usage?.PromptTokens ?? 0,
                    response.Usage?.CompletionTokens ?? 0),
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = choice != null,
                ErrorMessage = choice == null ? "No valid response from OpenAI" : null,
                Metadata = new Dictionary<string, object>
                {
                    ["openai_response_id"] = response.Id,
                    ["finish_reason"] = choice?.FinishReason,
                    ["system_fingerprint"] = response.SystemFingerprint
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

                var chunk = JsonSerializer.Deserialize<OpenAiStreamingChunk>(data, _jsonOptions);
                if (chunk?.Choices?.FirstOrDefault()?.Delta?.Content != null)
                {
                    yield return new StreamingChunk
                    {
                        Content = chunk.Choices.First().Delta.Content,
                        FinishReason = chunk.Choices.First().FinishReason,
                        Index = chunk.Choices.First().Index
                    };
                }
            }
        }

        private ModerationResponse ParseModerationResponse(
            OpenAiModerationResponse response,
            TimeSpan processingTime,
            ModerationRequest originalRequest)
        {
            var result = response.Results.FirstOrDefault();

            if (result == null)
            {
                throw new ProviderException("No moderation results returned", ProviderId);
            }

            return new ModerationResponse
            {
                Id = response.Id,
                Model = response.Model,
                Results = new List<ModerationResult>
            {
                new ModerationResult
                {
                    Categories = result.Categories,
                    CategoryScores = result.CategoryScores,
                    Flagged = result.Flagged,
                    Confidence = result.CategoryScores.Values.Max()
                }
            },
                InputTokens = 0, // OpenAI moderation doesn't return token counts
                Cost = 0m, // Usually free or included
                Timestamp = DateTime.UtcNow,
                ProcessingTime = processingTime,
                Success = true
            };
        }

        private async Task<EmbeddingResponse> ParseEmbeddingResponse(
            OpenAiEmbeddingResponse response,
            TimeSpan processingTime,
            EmbeddingRequest originalRequest)
        {
            var embedding = response.Data.FirstOrDefault();

            if (embedding == null)
            {
                throw new ProviderException("No embedding returned", ProviderId);
            }

            return new EmbeddingResponse
            {
                Id = response.Id,
                ModelId = response.Model,
                Provider = ProviderName,
                Embeddings = new List<List<float>> { embedding.Embedding },
                InputTokens = response.Usage?.PromptTokens ?? 0,
                TotalTokens = response.Usage?.TotalTokens ?? 0,
                Cost = await EstimateCostAsync(response.Usage?.PromptTokens ?? 0, 0),
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = true
            };
        }
    }

    // OpenAI DTOs
    public class OpenAiConfiguration
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.openai.com";
        public string OrganizationId { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
    }

    public class OpenAiChatRequest
    {
        public string Model { get; set; }
        public List<OpenAiMessage> Messages { get; set; } = new List<OpenAiMessage>();
        public int? MaxTokens { get; set; }
        public decimal? Temperature { get; set; }
        public decimal? TopP { get; set; }
        public int? N { get; set; } = 1;
        public bool? Stream { get; set; }
        public List<string> Stop { get; set; }
        public decimal? PresencePenalty { get; set; }
        public decimal? FrequencyPenalty { get; set; }
        public Dictionary<string, object> LogitBias { get; set; }
        public string User { get; set; }
    }

    public class OpenAiMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public string Name { get; set; }
    }

    public class OpenAiChatCompletionResponse
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<OpenAiChoice> Choices { get; set; } = new List<OpenAiChoice>();
        public OpenAiUsage Usage { get; set; }
        public string SystemFingerprint { get; set; }
    }

    public class OpenAiChoice
    {
        public int Index { get; set; }
        public OpenAiMessage Message { get; set; }
        public string FinishReason { get; set; }
    }

    public class OpenAiUsage
    {
        public int PromptTokens { get; set; }
        public int CompletionTokens { get; set; }
        public int TotalTokens { get; set; }
    }

    public class OpenAiStreamingChunk
    {
        public string Id { get; set; }
        public string Object { get; set; }
        public long Created { get; set; }
        public string Model { get; set; }
        public List<OpenAiStreamingChoice> Choices { get; set; } = new List<OpenAiStreamingChoice>();
    }

    public class OpenAiStreamingChoice
    {
        public int Index { get; set; }
        public OpenAiDelta Delta { get; set; }
        public string FinishReason { get; set; }
    }

    public class OpenAiDelta
    {
        public string Role { get; set; }
        public string Content { get; set; }
    }

    public class OpenAiModerationRequest
    {
        public string Input { get; set; }
        public string Model { get; set; }
    }

    public class OpenAiModerationResponse
    {
        public string Id { get; set; }
        public string Model { get; set; }
        public List<OpenAiModerationResult> Results { get; set; } = new List<OpenAiModerationResult>();
    }

    public class OpenAiModerationResult
    {
        public bool Flagged { get; set; }
        public Dictionary<string, bool> Categories { get; set; } = new Dictionary<string, bool>();
        public Dictionary<string, float> CategoryScores { get; set; } = new Dictionary<string, float>();
    }

    public class OpenAiEmbeddingRequest
    {
        public string Input { get; set; }
        public string Model { get; set; }
        public string EncodingFormat { get; set; }
        public string User { get; set; }
    }

    public class OpenAiEmbeddingResponse
    {
        public string Object { get; set; }
        public List<OpenAiEmbeddingData> Data { get; set; } = new List<OpenAiEmbeddingData>();
        public string Model { get; set; }
        public OpenAiUsage Usage { get; set; }
        public string Id { get; set; }
    }

    public class OpenAiEmbeddingData
    {
        public string Object { get; set; }
        public List<float> Embedding { get; set; } = new List<float>();
        public int Index { get; set; }
    }
}
