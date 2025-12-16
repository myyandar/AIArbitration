using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using AIArbitration.Infrastructure.Providers;
using AIArbitration.Infrastructure.Services;
using AIArbitration.Infrastructure.Services.ProviderSpecificAdapters;

namespace AIArbitration.Infrastructure.ServiceSupport
{
    // ServiceRegistration.cs
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAiProviders(this IServiceCollection services, IConfiguration configuration)
        {
            // Register configuration
            services.Configure<OpenAiConfiguration>(configuration.GetSection("Providers:OpenAI"));
            services.Configure<AzureOpenAiConfiguration>(configuration.GetSection("Providers:AzureOpenAI"));
            services.Configure<AnthropicConfiguration>(configuration.GetSection("Providers:Anthropic"));
            services.Configure<GeminiConfiguration>(configuration.GetSection("Providers:Google"));
            services.Configure<AwsBedrockConfiguration>(configuration.GetSection("Providers:AWS"));

            // Register HTTP clients with policies
            services.AddHttpClient("OpenAI")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddHttpClient("AzureOpenAI")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddHttpClient("Anthropic")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddHttpClient("Google")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddHttpClient("AWS")
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            // Register adapters
            services.AddScoped<OpenAiAdapter>();
            services.AddScoped<AzureOpenAiAdapter>();
            services.AddScoped<AnthropicAdapter>();
            services.AddScoped<GeminiAdapter>();
            services.AddScoped<AwsBedrockAdapter>();

            // Register factory
            services.AddScoped<IProviderAdapterFactory, ProviderAdapterFactory>();

            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                .WaitAndRetryAsync(3, retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        }
    }
}
