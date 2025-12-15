using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace AIArbitration.Infrastructure.Services
{
    internal class MockProviderAdapter : OllamaProviderAdapter
    {
        private AIArbitrationDbContext dbContext;
        private IHttpClientFactory httpClientFactory;
        private Microsoft.Extensions.Logging.ILogger<MockProviderAdapter> logger;
        private ICircuitBreaker circuitBreaker;
        private IRateLimiter rateLimiter;
        private IMemoryCache cache;
        private ModelProvider modelProvider;
        private ProviderConfiguration providerConfig;

        public MockProviderAdapter(AIArbitrationDbContext dbContext, IHttpClientFactory httpClientFactory, Microsoft.Extensions.Logging.ILogger<MockProviderAdapter> logger, ICircuitBreaker circuitBreaker, IRateLimiter rateLimiter, IMemoryCache cache, ModelProvider modelProvider, ProviderConfiguration providerConfig)
        {
            this.dbContext = dbContext;
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
            this.circuitBreaker = circuitBreaker;
            this.rateLimiter = rateLimiter;
            this.cache = cache;
            this.modelProvider = modelProvider;
            this.providerConfig = providerConfig;
        }
    }
}