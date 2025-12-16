using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    // GeminiAdapter.cs
    public class GeminiAdapter : BaseProviderAdapter
    {
        private readonly GeminiConfiguration _configuration;

        public override string ProviderId => "google-gemini";
        public override string ProviderName => "Google Gemini";

        public GeminiAdapter(
            HttpClient httpClient,
            ILogger<GeminiAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            GeminiConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            // Google Gemini uses API key in query string or header
            if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                var uriBuilder = new UriBuilder(request.RequestUri);
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                query["key"] = _configuration.ApiKey;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
            }

            request.Headers.UserAgent.ParseAdd("AIArbitrationEngine/1.0");
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            await CheckRateLimiterAsync($"gemini:{request.ModelId}");

            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var geminiRequest = MapToGeminiRequest(request);
                var endpoint = $"/v1beta/models/{request.ModelId}:generateContent";

                var httpRequest = CreateRequest(HttpMethod.Post, endpoint, geminiRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(content, _jsonOptions);

                return await ParseChatCompletionResponseAsync(geminiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private List<GeminiSafetySetting> GetSafetySettings()
        {
            // Default safety settings
            return new List<GeminiSafetySetting>
        {
            new GeminiSafetySetting { Category = "HARM_CATEGORY_HARASSMENT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
            new GeminiSafetySetting { Category = "HARM_CATEGORY_HATE_SPEECH", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
            new GeminiSafetySetting { Category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" },
            new GeminiSafetySetting { Category = "HARM_CATEGORY_DANGEROUS_CONTENT", Threshold = "BLOCK_MEDIUM_AND_ABOVE" }
        };
        }

        private async Task<ModelResponse> ParseChatCompletionResponseAsync(
            GeminiGenerateContentResponse response,
            TimeSpan processingTime,
            ChatRequest originalRequest)
        {
            var candidate = response.Candidates.FirstOrDefault();
            var text = candidate?.Content?.Parts?.FirstOrDefault()?.Text;

            return new ModelResponse
            {
                Id = Guid.NewGuid().ToString(), // Gemini doesn't return an ID
                ModelId = originalRequest.ModelId,
                Provider = ProviderName,
                Content = text,
                FinishReason = candidate?.FinishReason,
                InputTokens = response.UsageMetadata?.PromptTokenCount ?? 0,
                OutputTokens = response.UsageMetadata?.CandidatesTokenCount ?? 0,
                TotalTokens = response.UsageMetadata?.TotalTokenCount ?? 0,
                Cost = await EstimateCostAsync(
                    response.UsageMetadata?.PromptTokenCount ?? 0,
                    response.UsageMetadata?.CandidatesTokenCount ?? 0),
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = candidate != null && !string.IsNullOrEmpty(text),
                ErrorMessage = candidate == null ? "No valid response from Gemini" : null,
                Metadata = new Dictionary<string, object>
                {
                    ["safety_ratings"] = candidate?.SafetyRatings?.Count ?? 0,
                    ["prompt_feedback"] = response.PromptFeedback != null
                }
            };
        }

        public async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Gemini pricing (Gemini Pro example)
            var inputCostPer1K = 0.000125m; // $0.125 per 1M tokens = $0.000125 per 1K
            var outputCostPer1K = 0.000375m; // $0.375 per 1M tokens = $0.000375 per 1K

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }

        // Add this private helper method to GeminiAdapter to fix CS0103
        private async Task<T> ExecuteWithCircuitBreakerAsync<T>(Func<Task<T>> action, string operationName)
        {
            try
            {
                await CheckCircuitBreakerAsync();
                var result = await action();
                await RecordSuccessAsync();
                return result;
            }
            catch (Exception ex)
            {
                await RecordFailureAsync(ex);
                await HandleRequestErrorAsync(ex, null, operationName, null, DateTime.UtcNow);
                throw;
            }
        }

        // Add this private helper method to GeminiAdapter to fix CS0103
        private HttpRequestMessage CreateRequest(HttpMethod method, string endpoint, object body)
        {
            var request = new HttpRequestMessage(method, endpoint);
            if (body != null)
            {
                var json = JsonSerializer.Serialize(body, _jsonOptions);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }
            AddDefaultHeaders(request);
            return request;
        }

        // Add this method to GeminiAdapter to resolve CS0122 by providing a public or private mapping implementation
        private object MapToGeminiRequest(ChatRequest request)
        {
            // Map ChatRequest to the Gemini API request format.
            // This is a placeholder; adjust the mapping as needed for your actual Gemini API contract.
            return new
            {
                contents = request.Messages.Select(m => new
                {
                    role = m.Role,
                    parts = new[] { new { text = m.Content } }
                }).ToList(),
                safetySettings = GetSafetySettings(),
                generationConfig = new
                {
                    temperature = request.Temperature ?? 1.0m,
                    topP = request.TopP ?? 1.0m,
                    maxOutputTokens = request.MaxTokens ?? 1024
                }
            };
        }

        // Implement other required methods...
    }
}
