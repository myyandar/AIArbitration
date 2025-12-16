using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
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

                return ParseEmbeddingResponse(cohereResponse, stopwatch.Elapsed, request);
            }, "SendEmbeddingAsync");
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

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("Authorization", $"Bearer {_configuration.ApiKey}");
            request.Headers.Add("Request-Source", "AIArbitrationEngine");
        }

        private CohereChatRequest MapToCohereRequest(ChatRequest request)
        {
            return new CohereChatRequest
            {
                Model = request.ModelId,
                Message = request.Messages.LastOrDefault(m => m.Role == ChatRole.User)?.Content,
                ChatHistory = request.Messages
                    .Where(m => m.Role != ChatRole.System)
                    .Select(m => new CohereChatHistory
                    {
                        Role = m.Role == ChatRole.Assistant ? "CHATBOT" : "USER",
                        Message = m.Content
                    })
                    .ToList(),
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
                Id = response.Id,
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

        private EmbeddingResponse ParseEmbeddingResponse(
            CohereEmbeddingResponse response,
            TimeSpan processingTime,
            EmbeddingRequest originalRequest)
        {
            return new EmbeddingResponse
            {
                Id = response.Id,
                ModelId = response.Model,
                Provider = ProviderName,
                Embeddings = response.Embeddings,
                InputTokens = 0, // Cohere embeddings don't return token counts
                TotalTokens = 0,
                Cost = 0m, // Will be calculated separately
                ProcessingTime = processingTime,
                Timestamp = DateTime.UtcNow,
                Success = response.Embeddings?.Any() == true
            };
        }

        // Cohere doesn't have built-in moderation
        public override Task<ModerationResponse> SendModerationAsync(ModerationRequest request)
        {
            throw new NotSupportedException("Cohere does not provide a moderation API");
        }
    }

    // 2. Mistral AI Adapter
    public class MistralAdapter : BaseProviderAdapter
    {
        private readonly MistralConfiguration _configuration;

        public override string ProviderId => "mistral";
        public override string ProviderName => "Mistral AI";

        public MistralAdapter(
            HttpClient httpClient,
            ILogger<MistralAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            MistralConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var mistralRequest = MapToMistralRequest(request);
                var httpRequest = CreateRequest(
                    HttpMethod.Post,
                    _configuration.UseAzure ? "/v1/chat/completions" : "/v1/chat/completions",
                    mistralRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();

                // Mistral API is OpenAI-compatible
                var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);

                return await ParseMistralResponseAsync(openAiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private MistralChatRequest MapToMistralRequest(ChatRequest request)
        {
            return new MistralChatRequest
            {
                Model = request.ModelId,
                Messages = request.Messages.Select(m => new MistralMessage
                {
                    Role = m.Role.ToString().ToLower(),
                    Content = m.Content
                }).ToList(),
                MaxTokens = (int?)request.MaxTokens,
                Temperature = (float?)request.Temperature,
                TopP = (float?)request.TopP,
                Stream = false,
                SafePrompt = _configuration.UseSafePrompt
            };
        }

        private async Task<ModelResponse> ParseMistralResponseAsync(
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
                Success = choice != null
            };
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Mistral pricing
            var model = _configuration.DefaultModel ?? "mistral-medium";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "mistral-large" => (0.008m, 0.024m),      // $8/$24 per 1M tokens
                "mistral-medium" => (0.0027m, 0.0081m),  // $2.7/$8.1 per 1M tokens
                "mistral-small" => (0.002m, 0.006m),     // $2/$6 per 1M tokens
                "codestral" => (0.003m, 0.009m),         // $3/$9 per 1M tokens
                _ => (0.002m, 0.006m)                    // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }

        // Implement other required methods...
    }

    // 3. Groq Adapter (LLaMA models)
    public class GroqAdapter : BaseProviderAdapter
    {
        private readonly GroqConfiguration _configuration;

        public override string ProviderId => "groq";
        public override string ProviderName => "Groq";

        public GroqAdapter(
            HttpClient httpClient,
            ILogger<GroqAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            GroqConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                // Groq uses OpenAI-compatible API
                var openAiRequest = MapToOpenAiRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat/completions", openAiRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);

                return await ParseGroqResponseAsync(openAiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Groq pricing (very low due to hardware acceleration)
            var model = _configuration.DefaultModel ?? "llama3-70b-8192";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "llama3-70b-8192" => (0.00059m, 0.00079m),    // $0.59/$0.79 per 1M tokens
                "llama3-8b-8192" => (0.00005m, 0.00008m),    // $0.05/$0.08 per 1M tokens
                "mixtral-8x7b-32768" => (0.00024m, 0.00024m), // $0.24 per 1M tokens
                "gemma-7b-it" => (0.00007m, 0.00007m),       // $0.07 per 1M tokens
                _ => (0.0001m, 0.0001m)                      // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }
    }

    // 4. Perplexity AI Adapter
    public class PerplexityAdapter : BaseProviderAdapter
    {
        private readonly PerplexityConfiguration _configuration;

        public override string ProviderId => "perplexity";
        public override string ProviderName => "Perplexity AI";

        public PerplexityAdapter(
            HttpClient httpClient,
            ILogger<PerplexityAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            PerplexityConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                // Perplexity uses OpenAI-compatible API with search capability
                var perplexityRequest = MapToPerplexityRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/v1/chat/completions", perplexityRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);

                return await ParsePerplexityResponseAsync(openAiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private PerplexityChatRequest MapToPerplexityRequest(ChatRequest request)
        {
            return new PerplexityChatRequest
            {
                Model = request.ModelId,
                Messages = request.Messages.Select(m => new OpenAiMessage
                {
                    Role = m.Role.ToString().ToLower(),
                    Content = m.Content
                }).ToList(),
                MaxTokens = (int?)request.MaxTokens,
                Temperature = (float?)request.Temperature,
                SearchDepth = _configuration.SearchDepth
            };
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Perplexity pricing (search-enhanced)
            var model = _configuration.DefaultModel ?? "sonar-small-online";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "sonar-small-online" => (0.0002m, 0.002m),    // $0.2/$2 per 1M tokens
                "sonar-medium-online" => (0.0006m, 0.006m),  // $0.6/$6 per 1M tokens
                "llama-3-sonar-large-128k-online" => (0.001m, 0.01m), // $1/$10 per 1M tokens
                _ => (0.0005m, 0.005m)                       // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }
    }

    // 5. Replicate Adapter (Multiple models)
    public class ReplicateAdapter : BaseProviderAdapter
    {
        private readonly ReplicateConfiguration _configuration;

        public override string ProviderId => "replicate";
        public override string ProviderName => "Replicate";

        public ReplicateAdapter(
            HttpClient httpClient,
            ILogger<ReplicateAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            ReplicateConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Token", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                // Replicate uses model version IDs
                var modelVersion = await ResolveModelVersionAsync(request.ModelId);
                var replicateRequest = MapToReplicateRequest(request, modelVersion);

                var httpRequest = CreateRequest(HttpMethod.Post, $"/v1/predictions", replicateRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var predictionResponse = JsonSerializer.Deserialize<ReplicatePredictionResponse>(content, _jsonOptions);

                // Poll for completion (Replicate is async)
                var result = await WaitForPredictionCompletionAsync(predictionResponse.Id);
                stopwatch.Stop();

                return ParseReplicateResponse(result, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private async Task<string> ResolveModelVersionAsync(string modelId)
        {
            // Model ID format: "owner/model:version" or just "owner/model"
            if (modelId.Contains(':'))
                return modelId;

            // Get latest version
            var httpRequest = CreateRequest(HttpMethod.Get, $"/v1/models/{modelId}");
            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var modelInfo = JsonSerializer.Deserialize<ReplicateModelInfo>(content, _jsonOptions);

            return $"{modelId}:{modelInfo.LatestVersion.Id}";
        }

        private async Task<ReplicatePredictionResult> WaitForPredictionCompletionAsync(string predictionId)
        {
            var maxAttempts = 60; // 60 seconds max
            var delayMs = 1000;

            for (int i = 0; i < maxAttempts; i++)
            {
                var httpRequest = CreateRequest(HttpMethod.Get, $"/v1/predictions/{predictionId}");
                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<ReplicatePredictionResult>(content, _jsonOptions);

                if (result.Status == "succeeded" || result.Status == "failed")
                    return result;

                await Task.Delay(delayMs);
            }

            throw new TimeoutException($"Prediction {predictionId} timed out");
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Replicate pricing varies widely by model
            // This is an estimate - actual cost depends on model and compute time
            return 0.01m; // Default minimum cost per request
        }
    }

    // 6. Hugging Face Inference API
    public class HuggingFaceAdapter : BaseProviderAdapter
    {
        private readonly HuggingFaceConfiguration _configuration;

        public override string ProviderId => "huggingface";
        public override string ProviderName => "Hugging Face";

        public HuggingFaceAdapter(
            HttpClient httpClient,
            ILogger<HuggingFaceAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            HuggingFaceConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var hfRequest = MapToHuggingFaceRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, $"/models/{request.ModelId}", hfRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();

                // Hugging Face returns array of responses
                var responses = JsonSerializer.Deserialize<List<HuggingFaceResponse>>(content, _jsonOptions);

                return ParseHuggingFaceResponse(responses?.FirstOrDefault(), stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private HuggingFaceChatRequest MapToHuggingFaceRequest(ChatRequest request)
        {
            // Format depends on model - use generic format
            var messages = request.Messages.Select(m => $"{m.Role}: {m.Content}");
            var prompt = string.Join("\n", messages);

            return new HuggingFaceChatRequest
            {
                Inputs = prompt,
                Parameters = new HuggingFaceParameters
                {
                    MaxNewTokens = (int?)request.MaxTokens,
                    Temperature = (float?)request.Temperature,
                    TopP = (float?)request.TopP,
                    DoSample = request.Temperature > 0
                }
            };
        }
    }

    // 7. Together AI Adapter
    public class TogetherAdapter : BaseProviderAdapter
    {
        private readonly TogetherConfiguration _configuration;

        public override string ProviderId => "together";
        public override string ProviderName => "Together AI";

        public TogetherAdapter(
            HttpClient httpClient,
            ILogger<TogetherAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            TogetherConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            // Together AI uses OpenAI-compatible API
            return await ExecuteOpenAiCompatibleRequestAsync(request);
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Together AI pricing (very competitive)
            var model = _configuration.DefaultModel ?? "meta-llama/Meta-Llama-3-70B-Instruct";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                var m when m.Contains("llama-3-70b") => (0.0009m, 0.0009m),     // $0.9 per 1M tokens
                var m when m.Contains("llama-3-8b") => (0.00018m, 0.00018m),   // $0.18 per 1M tokens
                var m when m.Contains("mixtral") => (0.0006m, 0.0006m),        // $0.6 per 1M tokens
                var m when m.Contains("qwen") => (0.0001m, 0.0001m),           // $0.1 per 1M tokens
                _ => (0.0005m, 0.0005m)                                        // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }
    }

    // 8. OctoAI Adapter
    public class OctoAIAdapter : BaseProviderAdapter
    {
        private readonly OctoAIConfiguration _configuration;

        public override string ProviderId => "octoai";
        public override string ProviderName => "OctoAI";

        public OctoAIAdapter(
            HttpClient httpClient,
            ILogger<OctoAIAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            OctoAIConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            // OctoAI uses OpenAI-compatible API
            return await ExecuteOpenAiCompatibleRequestAsync(request);
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // OctoAI pricing
            return 0.002m * (inputTokens + outputTokens) / 1000m; // $2 per 1M tokens
        }
    }

    // 9. Vertex AI Adapter (Google Cloud)
    public class VertexAIAdapter : BaseProviderAdapter
    {
        private readonly VertexAIConfiguration _configuration;
        private readonly IGoogleCredentialProvider _credentialProvider;

        public override string ProviderId => "vertexai";
        public override string ProviderName => "Google Vertex AI";

        public VertexAIAdapter(
            HttpClient httpClient,
            ILogger<VertexAIAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            VertexAIConfiguration configuration,
            IGoogleCredentialProvider credentialProvider = null)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _credentialProvider = credentialProvider;
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            // Vertex AI uses Google Cloud authentication
            if (_credentialProvider != null)
            {
                var token = _credentialProvider.GetAccessToken();
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            else if (!string.IsNullOrEmpty(_configuration.ApiKey))
            {
                request.Headers.Add("x-goog-api-key", _configuration.ApiKey);
            }

            request.Headers.Add("x-goog-user-project", _configuration.ProjectId);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var vertexRequest = MapToVertexRequest(request);
                var endpoint = $"/v1/projects/{_configuration.ProjectId}/locations/{_configuration.Location}/publishers/google/models/{request.ModelId}:predict";

                var httpRequest = CreateRequest(HttpMethod.Post, endpoint, vertexRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var vertexResponse = JsonSerializer.Deserialize<VertexPredictResponse>(content, _jsonOptions);

                return await ParseVertexResponseAsync(vertexResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private VertexPredictRequest MapToVertexRequest(ChatRequest request)
        {
            var instances = new List<VertexInstance>
        {
            new VertexInstance
            {
                Messages = request.Messages.Select(m => new VertexMessage
                {
                    Author = m.Role.ToString().ToLower(),
                    Content = m.Content
                }).ToList()
            }
        };

            return new VertexPredictRequest
            {
                Instances = instances,
                Parameters = new VertexParameters
                {
                    MaxOutputTokens = (int?)request.MaxTokens,
                    Temperature = (float?)request.Temperature,
                    TopP = (float?)request.TopP
                }
            };
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Vertex AI pricing (similar to Gemini)
            var model = _configuration.DefaultModel ?? "gemini-pro";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "gemini-1.5-pro" => (0.0035m, 0.0105m),     // $3.5/$10.5 per 1M tokens
                "gemini-1.5-flash" => (0.000075m, 0.0003m), // $0.075/$0.3 per 1M tokens
                "gemini-pro" => (0.0005m, 0.0015m),         // $0.5/$1.5 per 1M tokens
                "text-bison" => (0.0005m, 0.001m),          // $0.5/$1 per 1M tokens
                _ => (0.001m, 0.002m)                       // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }
    }

    // 10. SambaNova Adapter
    public class SambaNovaAdapter : BaseProviderAdapter
    {
        private readonly SambaNovaConfiguration _configuration;

        public override string ProviderId => "sambanova";
        public override string ProviderName => "SambaNova";

        public SambaNovaAdapter(
            HttpClient httpClient,
            ILogger<SambaNovaAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            SambaNovaConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("key", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            // SambaNova uses OpenAI-compatible API
            return await ExecuteOpenAiCompatibleRequestAsync(request);
        }
    }

    // 11. Cerebras Adapter
    public class CerebrasAdapter : BaseProviderAdapter
    {
        private readonly CerebrasConfiguration _configuration;

        public override string ProviderId => "cerebras";
        public override string ProviderName => "Cerebras";

        public CerebrasAdapter(
            HttpClient httpClient,
            ILogger<CerebrasAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            CerebrasConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        // Cerebras uses OpenAI-compatible API
        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteOpenAiCompatibleRequestAsync(request);
        }
    }

    // 12. Aleph Alpha Adapter
    public class AlephAlphaAdapter : BaseProviderAdapter
    {
        private readonly AlephAlphaConfiguration _configuration;

        public override string ProviderId => "alephalpha";
        public override string ProviderName => "Aleph Alpha";

        public AlephAlphaAdapter(
            HttpClient httpClient,
            ILogger<AlephAlphaAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            AlephAlphaConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                var aaRequest = MapToAlephAlphaRequest(request);
                var httpRequest = CreateRequest(HttpMethod.Post, "/complete", aaRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();
                var aaResponse = JsonSerializer.Deserialize<AlephAlphaCompletionResponse>(content, _jsonOptions);

                return await ParseAlephAlphaResponseAsync(aaResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private AlephAlphaCompletionRequest MapToAlephAlphaRequest(ChatRequest request)
        {
            // Convert chat messages to prompt
            var prompt = string.Join("\n", request.Messages.Select(m =>
                $"{m.Role.ToString().ToUpper()}: {m.Content}"));

            return new AlephAlphaCompletionRequest
            {
                Model = request.ModelId,
                Prompt = prompt,
                MaximumTokens = (int?)request.MaxTokens,
                Temperature = (float?)request.Temperature,
                TopK = (int?)request.TopK,
                TopP = (float?)request.TopP,
                PresencePenalty = (float?)request.PresencePenalty,
                FrequencyPenalty = (float?)request.FrequencyPenalty
            };
        }
    }

    // 13. DeepSeek Adapter
    public class DeepSeekAdapter : BaseProviderAdapter
    {
        private readonly DeepSeekConfiguration _configuration;

        public override string ProviderId => "deepseek";
        public override string ProviderName => "DeepSeek";

        public DeepSeekAdapter(
            HttpClient httpClient,
            ILogger<DeepSeekAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            DeepSeekConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _configuration.ApiKey);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            // DeepSeek uses OpenAI-compatible API
            return await ExecuteOpenAiCompatibleRequestAsync(request);
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // DeepSeek pricing (very affordable)
            var model = _configuration.DefaultModel ?? "deepseek-chat";

            var (inputCostPer1K, outputCostPer1K) = model.ToLower() switch
            {
                "deepseek-chat" => (0.00014m, 0.00028m),    // $0.14/$0.28 per 1M tokens
                "deepseek-coder" => (0.00014m, 0.00028m),   // $0.14/$0.28 per 1M tokens
                _ => (0.0001m, 0.0002m)                     // Default
            };

            var inputCost = (inputTokens / 1000m) * inputCostPer1K;
            var outputCost = (outputTokens / 1000m) * outputCostPer1K;

            return inputCost + outputCost;
        }
    }

    // 14. Meta Llama Adapter (via various providers)
    public class MetaLlamaAdapter : BaseProviderAdapter
    {
        // This is a meta-adapter that can route to different providers
        // that host Llama models (Together AI, Replicate, etc.)

        private readonly MetaLlamaConfiguration _configuration;
        private readonly IProviderAdapterFactory _adapterFactory;

        public string ProviderId => "meta-llama";
        public string ProviderName => "Meta Llama";

        public MetaLlamaAdapter(
            HttpClient httpClient,
            ILogger<MetaLlamaAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            MetaLlamaConfiguration configuration,
            IProviderAdapterFactory adapterFactory)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            // Determine which provider to use for this Llama model
            var providerModelId = await ResolveLlamaProviderAsync(request.ModelId);

            // Use the appropriate adapter
            var adapter = await _adapterFactory.GetAdapterForModelAsync(providerModelId);
            return await adapter.SendChatCompletionAsync(request);
        }

        private async Task<string> ResolveLlamaProviderAsync(string modelId)
        {
            // Map Llama model to specific provider
            var mapping = new Dictionary<string, string>
            {
                ["llama-3-70b-instruct"] = "together:meta-llama/Meta-Llama-3-70B-Instruct",
                ["llama-3-8b-instruct"] = "together:meta-llama/Meta-Llama-3-8B-Instruct",
                ["llama-2-70b-chat"] = "replicate:meta/llama-2-70b-chat",
                ["codellama-70b"] = "together:codellama/CodeLlama-70b-Instruct-hf"
            };

            if (mapping.TryGetValue(modelId.ToLower(), out var providerModelId))
            {
                return providerModelId;
            }

            // Default to Together AI
            return $"together:meta-llama/{modelId}";
        }
    }

    // 15. Local LLM Adapter (Ollama, LM Studio, etc.)
    public class LocalLLMAdapter : BaseProviderAdapter
    {
        private readonly LocalLLMConfiguration _configuration;

        public override string ProviderId => "local-llm";
        public override string ProviderName => "Local LLM";

        public LocalLLMAdapter(
            HttpClient httpClient,
            ILogger<LocalLLMAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            LocalLLMConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            httpClient.BaseAddress = new Uri(_configuration.BaseUrl);
            httpClient.Timeout = TimeSpan.FromSeconds(_configuration.TimeoutSeconds);
        }

        public override async Task<ModelResponse> SendChatCompletionAsync(ChatRequest request)
        {
            return await ExecuteWithCircuitBreakerAsync(async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                // Local LLMs typically use OpenAI-compatible APIs
                var localRequest = MapToLocalRequest(request);
                var endpoint = _configuration.UseOllama ? "/api/generate" : "/v1/chat/completions";

                var httpRequest = CreateRequest(HttpMethod.Post, endpoint, localRequest);

                var response = await _httpClient.SendAsync(httpRequest);
                response.EnsureSuccessStatusCode();

                stopwatch.Stop();

                var content = await response.Content.ReadAsStringAsync();

                if (_configuration.UseOllama)
                {
                    var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(content, _jsonOptions);
                    return ParseOllamaResponse(ollamaResponse, stopwatch.Elapsed, request);
                }
                else
                {
                    var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(content, _jsonOptions);
                    return await ParseLocalOpenAiResponse(openAiResponse, stopwatch.Elapsed, request);
                }
            }, "SendChatCompletionAsync");
        }

        private object MapToLocalRequest(ChatRequest request)
        {
            if (_configuration.UseOllama)
            {
                return new OllamaRequest
                {
                    Model = request.ModelId,
                    Prompt = request.Messages.LastOrDefault()?.Content,
                    Stream = false,
                    Options = new OllamaOptions
                    {
                        Temperature = (float?)request.Temperature,
                        TopP = (float?)request.TopP,
                        NumPredict = (int?)request.MaxTokens
                    }
                };
            }
            else
            {
                // OpenAI-compatible
                return new OpenAiChatRequest
                {
                    Model = request.ModelId,
                    Messages = request.Messages.Select(m => new OpenAiMessage
                    {
                        Role = m.Role.ToString().ToLower(),
                        Content = m.Content
                    }).ToList(),
                    MaxTokens = (int?)request.MaxTokens,
                    Temperature = (decimal?)request.Temperature
                };
            }
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Local LLMs have zero API cost (only infrastructure cost)
            return 0m;
        }

        // Helper method for OpenAI-compatible providers
        private async Task<ModelResponse> ExecuteOpenAiCompatibleRequestAsync(ChatRequest request)
        {
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

                return await ParseOpenAiCompatibleResponseAsync(openAiResponse, stopwatch.Elapsed, request);
            }, "SendChatCompletionAsync");
        }

        private async Task<ModelResponse> ParseOpenAiCompatibleResponseAsync(
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
                Success = choice != null && !string.IsNullOrEmpty(choice.Message?.Content),
                ErrorMessage = choice == null ? $"No response from {ProviderName}" : null
            };
        }
    } 
}

