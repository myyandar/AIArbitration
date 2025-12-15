using AIArbitration.Core.Entities;
using AIArbitration.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIArbitration.Infrastructure.Interfaces
{
    public interface ICostEstimationService
    {
        Task<CostEstimation> EstimateCostAsync(ChatRequest request, ArbitrationContext context);
        Task<CostEstimation> EstimateCostForModelAsync(AIModel model, ArbitrationContext context);
        Task<CostEstimation> AggregateCostEstimations(List<CostEstimation> estimations, TokenEstimation tokenEstimation);
        Task<TokenEstimation> EstimateTokensAsync(ChatRequest request);
    }
}
