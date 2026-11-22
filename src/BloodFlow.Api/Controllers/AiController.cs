using BloodFlow.Application.Ai;
using BloodFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers
{
    /// <summary>
    /// Backend gateway to the BloodFlow AI service. The frontend calls these endpoints (with its JWT);
    /// the backend calls the AI service with the x-api-key, so the key never reaches the browser.
    /// </summary>
    [Route("api/ai")]
    [ApiController]
    [Authorize(Roles = StaffRoles)]
    public class AiController : ControllerBase
    {
        private const string StaffRoles = Roles.SystemAdmin + "," + Roles.HospitalStaff + "," + Roles.BloodBankStaff;

        private readonly IBloodFlowAiClient _ai;

        public AiController(IBloodFlowAiClient ai)
        {
            _ai = ai;
        }

        /// <summary>Is the AI service up, and which day is "today" for the model?</summary>
        [HttpGet("health")]
        [AllowAnonymous]
        public Task<IActionResult> Health(CancellationToken ct)
            => Run(() => _ai.GetHealthAsync(ct));

        /// <summary>Current stock per blood type, computed by the AI from the batches.</summary>
        [HttpGet("stock")]
        public Task<IActionResult> Stock(CancellationToken ct)
            => Run(() => _ai.GetStockAsync(ct));

        /// <summary>14-day consumption forecast per blood type with an 80% range (for the Dashboard chart).</summary>
        [HttpGet("forecast")]
        public Task<IActionResult> Forecast(CancellationToken ct)
            => Run(() => _ai.GetForecastAllAsync(ct));

        /// <summary>When each blood type will run short, alert level (RED/YELLOW/GREEN) and deficit. Body is optional.</summary>
        [HttpPost("shortage")]
        public Task<IActionResult> Shortage([FromBody] StockOverride? request, CancellationToken ct)
            => Run(() => _ai.PredictShortageAsync(request, ct));

        /// <summary>Units expected to expire unused in the next 14 days, with risk level.</summary>
        [HttpPost("expiry")]
        public Task<IActionResult> Expiry(CancellationToken ct)
            => Run(() => _ai.PredictExpiryAsync(ct));

        /// <summary>Compatible red-cell substitutions for types below the safety level. Suggestions only: staff approve them.</summary>
        [HttpPost("substitutions")]
        public Task<IActionResult> Substitutions([FromBody] StockOverride? request, CancellationToken ct)
            => Run(() => _ai.GetSubstitutionsAsync(request, ct));

        /// <summary>Forecast from a history sent in the request (does not change the saved models).</summary>
        [HttpPost("demand")]
        public Task<IActionResult> Demand([FromBody] DemandRequest request, CancellationToken ct)
            => Run(() => _ai.PredictDemandAsync(request, ct));

        /// <summary>Send new data and retrain the models (1-2 minutes). Admin only, one at a time.</summary>
        [HttpPost("retrain")]
        [Authorize(Roles = Roles.SystemAdmin)]
        public Task<IActionResult> Retrain([FromBody] RetrainRequest request, CancellationToken ct)
            => Run(() => _ai.RetrainAsync(request, ct));

        private async Task<IActionResult> Run<T>(Func<Task<T>> call)
        {
            try
            {
                return Ok(await call());
            }
            catch (AiServiceException ex)
            {
                var status = ex.Kind switch
                {
                    AiErrorKind.BadRequest => StatusCodes.Status400BadRequest,
                    AiErrorKind.Busy => StatusCodes.Status409Conflict,
                    AiErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
                    _ => StatusCodes.Status502BadGateway // Unauthorized (our key) or unexpected upstream failure
                };
                return Problem(title: "AI service error", detail: ex.Message, statusCode: status);
            }
        }
    }
}
