using AIArbitration.Infrastructure.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    public class ProviderAdapterFactory : IProviderAdapterFactory
    {
        private IProviderAdapter CreateAdapter(string providerName)
        {
            return providerName.ToLower() switch
            {
                "openai" => new OpenAiProviderAdapter(
                    _dbContext,
                    _httpClientFactory,
                    _loggerFactory.CreateLogger<OpenAiProviderAdapter>(),
                    _circuitBreaker,
                    _rateLimiter,
                    _cache,
                    provider,
                    configuration),

                "anthropic" => new AnthropicProviderAdapter(
                    _dbContext,
                    _httpClientFactory,
                    _loggerFactory.CreateLogger<AnthropicProviderAdapter>(),
                    _circuitBreaker,
                    _rateLimiter,
                    _cache,
                    provider,
                    configuration),

                "cohere" => new CohereProviderAdapter(
                    _dbContext,
                    _httpClientFactory,
                    _loggerFactory.CreateLogger<CohereProviderAdapter>(),
                    _circuitBreaker,
                    _rateLimiter,
                    _cache,
                    provider,
                    configuration),

                _ => throw new NotSupportedException($"Provider '{providerName}' is not supported")
            };

        }
    }
