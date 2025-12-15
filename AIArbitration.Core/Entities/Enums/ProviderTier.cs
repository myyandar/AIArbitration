namespace AIArbitration.Core.Entities.Enums
{
    public enum ProviderTier
    {
        Unknown = 0,
        Tier1,     // Major providers (OpenAI, Anthropic, Google, Microsoft)
        Tier2,     // Established providers (Cohere, AI21, etc.)
        Tier3,     // Specialized providers
        Tier4,     // Experimental/Research providers
        Community,  // Community/Open source providers
        Free = 6,
        Basic = 7,
        Professional = 8,
        Enterprise = 9
    }
}
