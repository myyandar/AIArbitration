using AIArbitration.Core;
using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface IRecordKeepingService
    {
        Task RecordArbitrationDecisionAsync(
            string decisionId,
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            List<ArbitrationCandidate> allCandidates,
            TimeSpan selectionDuration);

        Task RecordArbitrationFailureAsync(
            string decisionId,
            ArbitrationContext context,
            Exception exception,
            TimeSpan duration);

        Task RecordUsageAsync(
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            ModelResponse response,
            ChatRequest request);

        Task CheckBudgetWarningsAsync(ArbitrationContext context, decimal cost);

        Task HandleStreamingCompletionAsync(
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            int inputTokens,
            int outputTokens,
            decimal cost,
            TimeSpan processingTime);

        Task RecordExecutionSuccessAsync(
            string executionId,
            ArbitrationContext context,
            ArbitrationCandidate selectedModel,
            ModelResponse response,
            TimeSpan duration);

        Task RecordExecutionFailureAsync(
            string executionId,
            ArbitrationContext context,
            ChatRequest request,
            Exception exception,
            TimeSpan duration);
    }
}
