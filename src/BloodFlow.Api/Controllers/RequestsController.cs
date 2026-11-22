using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers
{
    /// <summary>
    /// Blood requests (Phase 1 sections 3–4, workflows H1–H3, B2–B3).
    /// Who may do what is decided from the token (role + org_id), never from the body.
    ///
    ///   Hospital:   POST /api/requests → GET /hospital → POST /{id}/receive
    ///   Blood bank: GET /board → POST /{id}/approve → POST /{id}/fulfil
    /// </summary>
    [ApiController]
    [Route("api/requests")]
    [Authorize(Roles = Roles.Staff)]
    [Produces("application/json")]
    public class RequestsController : ControllerBase
    {
        private readonly IRequestService _requests;

        public RequestsController(IRequestService requests)
        {
            _requests = requests;
        }

        /// <summary>
        /// Hospital staff create a request for THEIR hospital. neededBy is UTC.
        /// Send a new clientRequestId per submission so a retry never creates a second request.
        /// </summary>
        [HttpPost]
        [Authorize(Roles = Roles.HospitalStaff)]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(CreateBloodRequestRequest request, CancellationToken cancellationToken)
        {
            var result = await _requests.CreateAsync(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(Get), new { id = result.Value.Summary.Id }, result.Value)
                : this.ToProblem(result.Error!);
        }

        /// <summary>Requests whose destination is my hospital. onlyFlagged=true → overdue / unconfirmed / escalated.</summary>
        [HttpGet("hospital")]
        [ProducesResponseType<PagedResult<RequestSummaryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Hospital([FromQuery] RequestQuery query, CancellationToken cancellationToken) =>
            (await _requests.GetHospitalRequestsAsync(query, cancellationToken)).ToActionResult(this);

        /// <summary>Open requests my organization could fulfil: emergencies first, my governorate by default.</summary>
        [HttpGet("board")]
        [ProducesResponseType<PagedResult<RequestSummaryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Board([FromQuery] BoardQuery query, CancellationToken cancellationToken) =>
            (await _requests.GetBoardAsync(query, cancellationToken)).ToActionResult(this);

        /// <summary>Requests my organization reserved or fulfilled.</summary>
        [HttpGet("executing")]
        [ProducesResponseType<PagedResult<RequestSummaryDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Executing([FromQuery] RequestQuery query, CancellationToken cancellationToken) =>
            (await _requests.GetExecutingAsync(query, cancellationToken)).ToActionResult(this);

        [HttpGet("{id:int}")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(int id, CancellationToken cancellationToken) =>
            (await _requests.GetAsync(id, cancellationToken)).ToActionResult(this);

        [HttpGet("{id:int}/history")]
        [ProducesResponseType<IReadOnlyList<StatusHistoryDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> History(int id, CancellationToken cancellationToken) =>
            (await _requests.GetHistoryAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>The destination hospital verifies a request it did not create itself.</summary>
        [HttpPost("{id:int}/verify")]
        [Authorize(Roles = Roles.HospitalStaff)]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Verify(int id, CancellationToken cancellationToken) =>
            (await _requests.VerifyAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Approve = reserve the units now from my stock (FEFO). All units or nothing.</summary>
        [HttpPost("{id:int}/approve")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve(int id, CancellationToken cancellationToken) =>
            (await _requests.ApproveAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>Top up a reservation after a reserved batch expired.</summary>
        [HttpPost("{id:int}/reallocate")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reallocate(int id, CancellationToken cancellationToken) =>
            (await _requests.ReallocateAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// The blood leaves: reserved units are deducted from stock. An UNVERIFIED emergency needs
        /// emergencyOverrideReason (recorded and sent to the admin for review).
        /// </summary>
        [HttpPost("{id:int}/fulfil")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Fulfil(int id, FulfilRequestRequest request, CancellationToken cancellationToken) =>
            (await _requests.FulfilAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>The destination hospital confirms the blood arrived.</summary>
        [HttpPost("{id:int}/receive")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Receive(int id, CancellationToken cancellationToken) =>
            (await _requests.ReceiveAsync(id, cancellationToken)).ToActionResult(this);

        [HttpPost("{id:int}/reject")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject(int id, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _requests.RejectAsync(id, request.Reason, cancellationToken)).ToActionResult(this);

        /// <summary>Cancel a pending or approved request (an approved one releases its reserved units).</summary>
        [HttpPost("{id:int}/cancel")]
        [ProducesResponseType<RequestDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Cancel(int id, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _requests.CancelAsync(id, request.Reason, cancellationToken)).ToActionResult(this);
    }
}
