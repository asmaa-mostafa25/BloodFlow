using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    /// <summary>
    /// Workflow A3: the System Admin monitors all requests and reviews emergency overrides.
    /// </summary>
    [ApiController]
    [Route("api/admin")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class RequestsMonitorController : ControllerBase
    {
        private readonly IRequestAdminService _admin;
        private readonly IRequestService _requests;

        public RequestsMonitorController(IRequestAdminService admin, IRequestService requests)
        {
            _admin = admin;
            _requests = requests;
        }

        /// <summary>All requests. onlyFlagged=true → overdue, receipt not confirmed, or emergency waiting too long.</summary>
        [HttpGet("requests")]
        [ProducesResponseType<PagedResult<RequestSummaryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> All([FromQuery] RequestQuery query, CancellationToken cancellationToken) =>
            Ok(await _admin.GetAllAsync(query, cancellationToken));

        /// <summary>Cancel any pending or approved request (e.g. entered by mistake). Reserved units are released.</summary>
        [HttpPost("requests/{id:int}/cancel")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(int id, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _requests.CancelAsync(id, request.Reason, cancellationToken)).ToActionResult(this);

        /// <summary>Emergency overrides (default: the ones waiting for review).</summary>
        [HttpGet("emergency-overrides")]
        [ProducesResponseType<PagedResult<EmergencyOverrideDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Overrides([FromQuery] OverrideQuery query, CancellationToken cancellationToken) =>
            Ok(await _admin.GetOverridesAsync(query, cancellationToken));

        [HttpPost("emergency-overrides/{id:int}/review")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Review(int id, ReviewOverrideRequest request, CancellationToken cancellationToken) =>
            (await _admin.ReviewOverrideAsync(id, request.Notes, cancellationToken)).ToActionResult(this);
    }
}
