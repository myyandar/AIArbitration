using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface ICandidateSelectionService
    {
        Task<List<ArbitrationCandidate>> GetCandidatesAsync(ArbitrationContext context);
        Task<ArbitrationCandidate> CreateCandidateAsync(AIModel model, ArbitrationContext context);
        Task<List<ArbitrationCandidate>> ScoreAndRankCandidatesAsync(List<ArbitrationCandidate> candidates, ArbitrationContext context);
        List<ArbitrationCandidate> ApplyBusinessRules(List<ArbitrationCandidate> candidates, ArbitrationContext context);
        ArbitrationCandidate SelectBestModel(List<ArbitrationCandidate> candidates, ArbitrationContext context);
        List<ArbitrationCandidate> PrepareFallbackCandidates(List<ArbitrationCandidate> candidates, ArbitrationCandidate selectedModel);
        Task<bool> IsModelEligibleAsync(AIModel model, ArbitrationContext context);
    }
}
