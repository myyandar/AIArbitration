using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Core.Models
{
    public abstract class BaseProviderConfiguration
    {
        public string ApiKey { get; set; }
        public string BaseUrl { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public int MaxRetries { get; set; } = 3;
        public string DefaultModel { get; set; }
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
    }

    // Configuration classes for each provider
    public class CohereConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "command-r-plus";
    }

    public class MistralConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "mistral-medium";
        public bool UseAzure { get; set; }
        public bool UseSafePrompt { get; set; } = true;
    }

    public class GroqConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "llama3-70b-8192";
    }

    public class PerplexityConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "sonar-small-online";
        public string SearchDepth { get; set; } = "medium";
    }

    public class ReplicateConfiguration : BaseProviderConfiguration
    {
        public int PollingIntervalMs { get; set; } = 1000;
        public int MaxPollingAttempts { get; set; } = 60;
    }

    public class HuggingFaceConfiguration : BaseProviderConfiguration
    {
        public bool UseInferenceEndpoint { get; set; } = true;
    }

    public class TogetherConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "meta-llama/Meta-Llama-3-70B-Instruct";
    }

    public class OctoAIConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "llama-2-70b-chat";
    }

    public class VertexAIConfiguration : BaseProviderConfiguration
    {
        public string ProjectId { get; set; }
        public string Location { get; set; } = "us-central1";
        public string DefaultModel { get; set; } = "gemini-pro";
    }

    public class SambaNovaConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "llama-2-70b-chat";
    }

    public class CerebrasConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "llama-2-70b";
    }

    public class AlephAlphaConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "luminous-extended";
    }

    public class DeepSeekConfiguration : BaseProviderConfiguration
    {
        public string DefaultModel { get; set; } = "deepseek-chat";
    }

    public class MetaLlamaConfiguration : BaseProviderConfiguration
    {
        public string PreferredProvider { get; set; } = "together";
    }

    public class LocalLLMConfiguration : BaseProviderConfiguration
    {
        public bool UseOllama { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 300; // Longer timeout for local models
    }
}
