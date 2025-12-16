using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services.ProviderSpecificAdapters
{
    // AzureOpenAiAdapter.cs
    public class AzureOpenAiAdapter : OpenAiAdapter
    {
        private readonly AzureOpenAiConfiguration _azureConfig;

        public override string ProviderId => "azure-openai";
        public override string ProviderName => "Azure OpenAI";

        public AzureOpenAiAdapter(
            HttpClient httpClient,
            ILogger<AzureOpenAiAdapter> logger,
            ICircuitBreaker circuitBreaker,
            IRateLimiter rateLimiter,
            AzureOpenAiConfiguration configuration)
            : base(httpClient, logger, circuitBreaker, rateLimiter,
                  new OpenAiConfiguration
                  {
                      ApiKey = configuration.ApiKey,
                      BaseUrl = configuration.BaseUrl
                  })
        {
            _azureConfig = configuration;
        }

        protected override void AddDefaultHeaders(HttpRequestMessage request)
        {
            request.Headers.Add("api-key", _azureConfig.ApiKey);

            if (!string.IsNullOrEmpty(_azureConfig.ApiVersion))
            {
                // Azure OpenAI often requires API version in query string
                var uriBuilder = new UriBuilder(request.RequestUri);
                var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
                query["api-version"] = _azureConfig.ApiVersion;
                uriBuilder.Query = query.ToString();
                request.RequestUri = uriBuilder.Uri;
            }

            request.Headers.UserAgent.ParseAdd("AIArbitrationEngine/1.0");
        }

        public override async Task<decimal> EstimateCostAsync(int inputTokens, int outputTokens)
        {
            // Azure OpenAI pricing is similar to OpenAI but may have different rates
            // Check deployment-specific pricing
            var deployment = _azureConfig.Deployments.FirstOrDefault(d => d.ModelId == _azureConfig.DefaultDeployment);

            if (deployment != null)
            {
                var inputCostPer1K = deployment.InputCostPer1K;
                var outputCostPer1K = deployment.OutputCostPer1K;

                var inputCost = (inputTokens / 1000m) * inputCostPer1K;
                var outputCost = (outputTokens / 1000m) * outputCostPer1K;

                return inputCost + outputCost;
            }

            // Fallback to standard OpenAI pricing
            return await base.EstimateCostAsync(inputTokens, outputTokens);
        }

        public override async Task<ModelCapabilities> GetCapabilitiesAsync()
        {
            var baseCapabilities = await base.GetCapabilitiesAsync();

            return new ModelCapabilities
            {
                ProviderId = ProviderId,
                SupportsChatCompletion = true,
                SupportsStreaming = true,
                SupportsFunctionCalling = true,
                SupportsVision = _azureConfig.Deployments.Any(d => d.SupportsVision),
                SupportsAudio = _azureConfig.Deployments.Any(d => d.SupportsAudio),
                MaxContextLength = _azureConfig.Deployments.Max(d => d.MaxContextLength) ?? 128000,
                AvailableModels = _azureConfig.Deployments.Select(d => d.ModelId).ToList(),
                UpdatedAt = DateTime.UtcNow
            };
        }
    }

    // Azure OpenAI DTOs
    public class AzureOpenAiConfiguration
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; } // e.g., "https://{resource}.openai.azure.com/openai/deployments/{deployment}"
        public string ApiVersion { get; set; } = "2023-12-01-preview";
        public string DefaultDeployment { get; set; }
        public List<AzureDeployment> Deployments { get; set; } = new List<AzureDeployment>();
    }

    public class AzureDeployment
    {
        public string Name { get; set; }
        public string ModelId { get; set; }
        public int? MaxContextLength { get; set; }
        public decimal InputCostPer1K { get; set; }
        public decimal OutputCostPer1K { get; set; }
        public bool SupportsVision { get; set; }
        public bool SupportsAudio { get; set; }
        public bool SupportsFunctionCalling { get; set; }
        public List<string> Capabilities { get; set; } = new List<string>();
    }
}
