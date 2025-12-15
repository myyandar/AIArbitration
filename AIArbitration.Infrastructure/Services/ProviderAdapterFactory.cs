using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Services.ProviderSpecificAdapters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.RateLimiting;

namespace AIArbitration.Infrastructure.Services
{
    public class ProviderAdapterFactory : IProviderAdapterFactory
    {
        private readonly ILogger<ProviderAdapterFactory> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly ConcurrentDictionary<string, IProviderAdapter> _adapters = new();

        public ProviderAdapterFactory(ILogger<ProviderAdapterFactory> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public IProviderAdapter GetAdapter(string providerName)
        {
            if (_adapters.TryGetValue(providerName.ToLower(), out var adapter))
            {
                return adapter;
            }

            adapter = CreateAdapter(providerName);
            _adapters[providerName.ToLower()] = adapter;
            return adapter;
        }

        public async Task<IProviderAdapter> GetAdapterForModelAsync(string modelId)
        {
            var parts = modelId.Split('-');
            if (parts.Length < 2)
            {
                throw new InvalidOperationException($"Invalid modelId format: {modelId}");
            }

            var providerName = parts[0];
            return GetAdapter(providerName);
        }

        public async Task<List<IProviderAdapter>> GetActiveAdaptersAsync()
        {
            return _adapters.Values.ToList();
        }

        public async Task<bool> IsProviderAvailableAsync(string providerName)
        {
            try
            {
                var adapter = GetAdapter(providerName);
                var health = await adapter.CheckHealthAsync();
                return health == ProviderHealthStatus.Healthy;
            }
            catch
            {
                return false;
            }
        }

        public async Task<Dictionary<string, bool>> GetProvidersAvailabilityAsync()
        {
            var result = new Dictionary<string, bool>();
            foreach (var providerName in _adapters.Keys)
            {
                var isAvailable = await IsProviderAvailableAsync(providerName);
                result[providerName] = isAvailable;
            }
            return result;
        }

        private IProviderAdapter CreateAdapter(string providerName)
        {
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var loggerFactory = _serviceProvider.GetRequiredService<ILoggerFactory>();
            var dbContext = _serviceProvider.GetRequiredService<AIArbitrationDbContext>();
            var circuitBreaker = _serviceProvider.GetRequiredService<ICircuitBreaker>();
            var rateLimiter = _serviceProvider.GetRequiredService<IRateLimiter>();
            var cache = _serviceProvider.GetRequiredService<IMemoryCache>();
            var configuration = _serviceProvider.GetRequiredService<IConfiguration>();

            // Get provider configuration from database or configuration
            var providerConfig = GetProviderConfiguration(providerName, configuration);

            return providerName.ToLower() switch
            {
                "openai" => new OpenAiProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<OpenAiProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "anthropic" => new AnthropicProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<AnthropicProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "cohere" => new CohereProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<CohereProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "google" or "gemini" => new GoogleGeminiProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<GoogleGeminiProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider("google"), providerConfig),

                "mistral" => new MistralProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<MistralProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "deepseek" => new DeepSeekProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<DeepSeekProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "microsoft" or "azure" => new MicrosoftAzureOpenAiProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<MicrosoftAzureOpenAiProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider("microsoft"), providerConfig),

                "groq" => new GroqProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<GroqProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                "amazon" or "bedrock" => new AmazonBedrockProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<AmazonBedrockProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider("amazon"), providerConfig),

                "ollama" => new OllamaProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<OllamaProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig),

                _ => new MockProviderAdapter(
                    dbContext, httpClientFactory,
                    loggerFactory.CreateLogger<MockProviderAdapter>(),
                    circuitBreaker, rateLimiter, cache,
                    GetModelProvider(providerName), providerConfig)
            };
        }

        private ModelProvider GetModelProvider(string providerName)
        {
            // In production, this would fetch from database
            return new ModelProvider
            {
                Id = Guid.NewGuid().ToString(),
                Name = providerName,
                Code = providerName.ToLower(),
                DisplayName = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(providerName),
                BaseUrl = GetDefaultBaseUrl(providerName),
                IsEnabled = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        private ProviderConfiguration GetProviderConfiguration(string providerName, IConfiguration configuration)
        {
            return new ProviderConfiguration
            {
                Id = Guid.NewGuid().ToString(),
                ProviderId = providerName,
                ApiKey = configuration[$"Providers:{providerName}:ApiKey"] ?? "",
                ApiSecret = configuration[$"Providers:{providerName}:ApiSecret"] ?? "",
                BaseUrl = configuration[$"Providers:{providerName}:BaseUrl"] ?? GetDefaultBaseUrl(providerName),
                Timeout = TimeSpan.FromSeconds(30),
                MaxRetries = 3,
                RetryDelay = TimeSpan.FromSeconds(1),
                ServiceFeePercentage = 0.1m,
                EnableCircuitBreaker = true,
                EnableRateLimiting = true,
                RequestsPerMinute = 60,
                DefaultMaxTokens = 2048,
                DefaultInputTokenPrice = 0.001m,
                DefaultOutputTokenPrice = 0.002m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        private string GetDefaultBaseUrl(string providerName)
        {
            return providerName.ToLower() switch
            {
                "openai" => "https://api.openai.com/v1",
                "anthropic" => "https://api.anthropic.com/v1",
                "cohere" => "https://api.cohere.com/v1",
                "google" or "gemini" => "https://generativelanguage.googleapis.com",
                "mistral" => "https://api.mistral.ai/v1",
                "deepseek" => "https://api.deepseek.com",
                "microsoft" or "azure" => "https://{resource}.openai.azure.com",
                "groq" => "https://api.groq.com/openai/v1",
                "amazon" or "bedrock" => "https://bedrock-runtime.us-east-1.amazonaws.com",
                "ollama" => "http://localhost:11434",
                _ => "https://mock.example.com"
            };
        }
    }

    internal class MockProviderAdapter : OllamaProviderAdapter, IProviderAdapter
    {
        public MockProviderAdapter(
            AIArbitrationDbContext dbContext,
            IHttpClientFactory httpClientFactory,
            ILogger<OllamaProviderAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            IMemoryCache cache,
            ModelProvider modelProvider,
            ProviderConfiguration providerConfig)
            : base(
                dbContext,
                httpClientFactory,
                logger,
                circuitBreaker,
                rateLimiter,
                cache,
                modelProvider,
                providerConfig)
        {
            // Additional initialization for MockProviderAdapter if needed
        }
    }
}
