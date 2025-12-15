using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IExecutionService
    {
        Task<ModelResponse> ExecuteAsync(ChatRequest request, ArbitrationContext context);
        Task<StreamingModelResponse> ExecuteStreamingAsync(ChatRequest request, ArbitrationContext context);
        Task<BatchExecutionResult> ExecuteBatchAsync(List<ChatRequest> requests, ArbitrationContext context);
    }
}
