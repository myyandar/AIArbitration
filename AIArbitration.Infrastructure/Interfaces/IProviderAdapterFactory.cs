using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IProviderAdapterFactory
    {
        IProviderAdapter GetAdapter(string providerName);
        Task<IProviderAdapter> GetAdapterForModelAsync(string modelId);
        Task<List<IProviderAdapter>> GetActiveAdaptersAsync();
        Task<bool> IsProviderAvailableAsync(string providerName);
        Task<Dictionary<string, bool>> GetProvidersAvailabilityAsync();
    }
}
