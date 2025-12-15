using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IFallbackService
    {
        Task<ModelResponse> TryFallbackExecutionAsync(ChatRequest request, ArbitrationContext context, Exception originalException);
    }
}
