using AIArbitration.Core.Entities;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    public class DynamicProviderAdapterFactory : IProviderAdapterFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Dictionary<string, Type> _adapterTypes = new();

        public DynamicProviderAdapterFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            DiscoverAndRegisterAdapters();
        }

        private void DiscoverAndRegisterAdapters()
        {
            // Auto-discover all adapters in assembly
            var adapterType = typeof(BaseProviderAdapter);
            var adapterTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => !t.IsAbstract && adapterType.IsAssignableFrom(t))
                .ToList();

            foreach (var type in adapterTypes)
            {
                var instance = Activator.CreateInstance(type,
                    new object[] { null, null, null, null, null }) as BaseProviderAdapter;
                if (instance != null)
                {
                    _adapterTypes[instance.ProviderId.ToLower()] = type;
                }
            }

            // Also support plugin adapters
            LoadPluginAdapters();
        }

        public async Task<IProviderAdapter> GetAdapterForModelAsync(string providerModelId)
        {
            var model = await GetModelFromRepository(providerModelId);
            var provider = model.Provider;

            var providerType = provider.Type.ToLower(); // "openai", "anthropic", etc.

            if (_adapterTypes.TryGetValue(providerType, out var adapterType))
            {
                return CreateAdapter(adapterType, provider);
            }

            // Try to find by provider name pattern
            var matchingAdapter = _adapterTypes
                .FirstOrDefault(kv => providerType.Contains(kv.Key) || kv.Key.Contains(providerType));

            if (matchingAdapter.Value != null)
            {
                return CreateAdapter(matchingAdapter.Value, provider);
            }

            throw new NotSupportedException($"No adapter found for provider type: {providerType}");
        }

        private IProviderAdapter CreateAdapter(Type adapterType, ModelProvider provider)
        {
            // Get configuration from provider
            var config = provider.GetConfiguration();

            // Resolve dependencies
            var httpClientFactory = _serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient(provider.Name);

            var circuitBreaker = _serviceProvider.GetRequiredService<ICircuitBreaker>();
            var rateLimiter = _serviceProvider.GetRequiredService<IRateLimiter>();
            var loggerType = typeof(ILogger<>).MakeGenericType(adapterType);
            var logger = _serviceProvider.GetRequiredService(loggerType) as ILogger;

            // Create adapter instance with dynamic parameters
            var parameters = new object[]
            {
            httpClient,
            logger,
            circuitBreaker,
            rateLimiter,
            config
            };

            return Activator.CreateInstance(adapterType, parameters) as IProviderAdapter;
        }

        private void LoadPluginAdapters()
        {
            // Load adapters from plugins directory
            var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
            if (!Directory.Exists(pluginsPath)) return;

            foreach (var dll in Directory.GetFiles(pluginsPath, "*.dll"))
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var adapterTypes = assembly.GetTypes()
                        .Where(t => !t.IsAbstract && typeof(BaseProviderAdapter).IsAssignableFrom(t));

                    foreach (var type in adapterTypes)
                    {
                        _adapterTypes[type.Name.ToLower().Replace("adapter", "")] = type;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin {dll}: {ex.Message}");
                }
            }
        }
    }
}
