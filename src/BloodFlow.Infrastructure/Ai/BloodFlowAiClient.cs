using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BloodFlow.Application.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BloodFlow.Infrastructure.Ai
{
    public class BloodFlowAiClient : IBloodFlowAiClient
    {
        public const string HttpClientName = "BloodFlowAi";
        public const string RetrainHttpClientName = "BloodFlowAi.Retrain";

        internal static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = new AiJsonNamingPolicy(),
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true
        };

        // The AI service handles one retrain at a time. Shared across all client instances.
        private static readonly SemaphoreSlim RetrainLock = new(1, 1);

        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BloodFlowAiClient> _logger;

        public BloodFlowAiClient(IHttpClientFactory httpClientFactory, ILogger<BloodFlowAiClient> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public Task<AiHealth> GetHealthAsync(CancellationToken ct = default)
            => SendAsync<AiHealth>(HttpMethod.Get, "/health", null, HttpClientName, ct);

        public Task<AiStock> GetStockAsync(CancellationToken ct = default)
            => SendAsync<AiStock>(HttpMethod.Get, "/stock", null, HttpClientName, ct);

        public Task<Dictionary<string, List<ForecastPoint>>> GetForecastAllAsync(CancellationToken ct = default)
            => SendAsync<Dictionary<string, List<ForecastPoint>>>(HttpMethod.Get, "/forecast/all", null, HttpClientName, ct);

        public Task<List<ShortagePrediction>> PredictShortageAsync(StockOverride? stocks = null, CancellationToken ct = default)
            => SendAsync<List<ShortagePrediction>>(HttpMethod.Post, "/predict/shortage", StocksBody(stocks), HttpClientName, ct);

        public Task<List<ExpiryPrediction>> PredictExpiryAsync(CancellationToken ct = default)
            => SendAsync<List<ExpiryPrediction>>(HttpMethod.Post, "/predict/expiry", null, HttpClientName, ct);

        public Task<SubstitutionsResult> GetSubstitutionsAsync(StockOverride? stocks = null, CancellationToken ct = default)
            => SendAsync<SubstitutionsResult>(HttpMethod.Post, "/substitutions", StocksBody(stocks), HttpClientName, ct);

        public Task<DemandForecast> PredictDemandAsync(DemandRequest request, CancellationToken ct = default)
            => SendAsync<DemandForecast>(HttpMethod.Post, "/predict/demand", request, HttpClientName, ct);

        public async Task<RetrainResult> RetrainAsync(RetrainRequest request, CancellationToken ct = default)
        {
            if (!await RetrainLock.WaitAsync(0, ct))
            {
                throw new AiServiceException(AiErrorKind.Busy, "A retrain is already running. Try again in a few minutes.");
            }
            try
            {
                return await SendAsync<RetrainResult>(HttpMethod.Post, "/retrain", request, RetrainHttpClientName, ct);
            }
            finally
            {
                RetrainLock.Release();
            }
        }

        // Only send a body when there is something in it; an empty body means "use the stock computed from batches".
        private static object? StocksBody(StockOverride? stocks)
            => stocks?.Stocks is { Count: > 0 } ? stocks : null;

        private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, string clientName, CancellationToken ct)
        {
            var http = _httpClientFactory.CreateClient(clientName);
            using var request = new HttpRequestMessage(method, path.TrimStart('/'));
            if (body is not null)
            {
                // Serialized up front so Content-Length is sent (no chunked upload).
                var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
                request.Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await http.SendAsync(request, ct);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "AI service unreachable at {Path}", path);
                throw new AiServiceException(AiErrorKind.Unavailable,
                    "The AI service is not reachable. Make sure it is running and has finished starting.", null, ex);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogError(ex, "AI service timed out at {Path}", path);
                throw new AiServiceException(AiErrorKind.Unavailable, "The AI service took too long to respond.", null, ex);
            }

            using (response)
            {
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
                    return result ?? throw new AiServiceException(AiErrorKind.Failed, "The AI service returned an empty response.", (int)response.StatusCode);
                }

                var detail = await ReadDetailAsync(response, ct);
                var status = (int)response.StatusCode;
                _logger.LogWarning("AI service returned {Status} for {Path}: {Detail}", status, path, detail);

                throw response.StatusCode switch
                {
                    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity
                        => new AiServiceException(AiErrorKind.BadRequest, detail, status),
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        => new AiServiceException(AiErrorKind.Unauthorized,
                            "The backend's AI API key is missing or wrong. Check BloodFlowAi:ApiKey.", status),
                    HttpStatusCode.ServiceUnavailable or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout
                        => new AiServiceException(AiErrorKind.Unavailable, "The AI service is not available right now.", status),
                    _ => new AiServiceException(AiErrorKind.Failed, $"The AI service failed ({status}).", status)
                };
            }
        }

        // FastAPI errors are {"detail": "..."}; 422 errors have a list in "detail".
        private static async Task<string> ReadDetailAsync(HttpResponseMessage response, CancellationToken ct)
        {
            var text = await response.Content.ReadAsStringAsync(ct);
            try
            {
                using var doc = JsonDocument.Parse(text);
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("detail", out var detail))
                {
                    if (detail.ValueKind == JsonValueKind.String)
                    {
                        return detail.GetString()!;
                    }
                    if (detail.ValueKind == JsonValueKind.Array)
                    {
                        var parts = detail.EnumerateArray().Select(e =>
                        {
                            var msg = e.TryGetProperty("msg", out var m) ? m.GetString() : e.ToString();
                            var loc = e.TryGetProperty("loc", out var l) && l.ValueKind == JsonValueKind.Array
                                ? string.Join(".", l.EnumerateArray().Select(x => x.ToString()))
                                : null;
                            return loc is null ? msg : $"{loc}: {msg}";
                        });
                        return string.Join("; ", parts);
                    }
                    return detail.ToString();
                }
            }
            catch (JsonException)
            {
                // Not JSON; fall through to the raw text.
            }
            return string.IsNullOrWhiteSpace(text) ? response.ReasonPhrase ?? "Unknown error" : text;
        }
    }
}
