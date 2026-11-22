namespace BloodFlow.Application.Ai
{
    /// <summary>Talks to the BloodFlow AI service (FastAPI). See the AI API contract for each endpoint.</summary>
    public interface IBloodFlowAiClient
    {
        Task<AiHealth> GetHealthAsync(CancellationToken ct = default);
        Task<AiStock> GetStockAsync(CancellationToken ct = default);
        Task<Dictionary<string, List<ForecastPoint>>> GetForecastAllAsync(CancellationToken ct = default);
        Task<List<ShortagePrediction>> PredictShortageAsync(StockOverride? stocks = null, CancellationToken ct = default);
        Task<List<ExpiryPrediction>> PredictExpiryAsync(CancellationToken ct = default);
        Task<SubstitutionsResult> GetSubstitutionsAsync(StockOverride? stocks = null, CancellationToken ct = default);
        Task<DemandForecast> PredictDemandAsync(DemandRequest request, CancellationToken ct = default);

        /// <summary>Heavy (1-2 minutes). Only one retrain runs at a time; a second call while one is running throws AiServiceException with Kind = Busy.</summary>
        Task<RetrainResult> RetrainAsync(RetrainRequest request, CancellationToken ct = default);
    }
}
