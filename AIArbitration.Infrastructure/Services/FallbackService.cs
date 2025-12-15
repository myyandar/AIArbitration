using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using AIArbitration.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using AIArbitration.Infrastructure.Interfaces;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Services
{
    // Corrected FallbackService with IProviderAdapterFactory
    public class FallbackService : IFallbackService
    {
        private readonly IProviderAdapterFactory _adapterFactory; // Changed from IAdapterFactory
        private readonly ICandidateSelectionService _candidateSelectionService;
        private readonly ILogger<FallbackService> _logger;

        public FallbackService(
            IProviderAdapterFactory adapterFactory, // Changed from IAdapterFactory
            ICandidateSelectionService candidateSelectionService,
            ILogger<FallbackService> logger)
        {
            _adapterFactory = adapterFactory ?? throw new ArgumentNullException(nameof(adapterFactory));
            _candidateSelectionService = candidateSelectionService ?? throw new ArgumentNullException(nameof(candidateSelectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ModelResponse> TryFallbackExecutionAsync(
            ChatRequest request,
            ArbitrationContext context,
            Exception originalException)
        {
            _logger.LogInformation("Attempting fallback execution after failure: {Error}", originalException.Message);

            try
            {
                var candidates = await _candidateSelectionService.GetCandidatesAsync(context);

                if (!candidates.Any())
                {
                    _logger.LogWarning("No fallback candidates available");
                    throw new AllModelsFailedException("No fallback models available", originalException);
                }

                var scoredCandidates = await _candidateSelectionService.ScoreAndRankCandidatesAsync(candidates, context);
                var filteredCandidates = _candidateSelectionService.ApplyBusinessRules(scoredCandidates, context);

                int attempt = 0;
                int maxAttempts = context.MaxFallbackAttempts ?? 3;

                foreach (var candidate in filteredCandidates)
                {
                    if (attempt >= maxAttempts) break;

                    attempt++;
                    try
                    {
                        _logger.LogInformation(
                            "Fallback attempt {Attempt}: trying model {ModelId}",
                            attempt, candidate.Model.ProviderModelId);

                        var adapter = await _adapterFactory.GetAdapterForModelAsync(candidate.Model.ProviderModelId);
                        var response = await adapter.SendChatCompletionAsync(request);

                        _logger.LogInformation(
                            "Fallback successful with model {ModelId} on attempt {Attempt}",
                            candidate.Model.ProviderModelId, attempt);

                        return response;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Fallback model {ModelId} failed on attempt {Attempt}",
                            candidate.Model.ProviderModelId, attempt);
                    }
                }

                throw new AllModelsFailedException(
                    $"All {attempt} fallback attempts failed",
                    originalException);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "All fallback attempts failed");
                throw new AllModelsFailedException("All fallback attempts failed", ex);
            }
        }
    }
}
