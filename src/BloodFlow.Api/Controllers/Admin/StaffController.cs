using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    /// <summary>Workflow A1: the System Admin reviews and manages staff accounts.</summary>
    [ApiController]
    [Route("api/admin/staff")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class StaffController : ControllerBase
    {
        private readonly IStaffAdminService _staff;

        public StaffController(IStaffAdminService staff)
        {
            _staff = staff;
        }

        /// <summary>List staff accounts. Use status=Pending for the approval queue (oldest first).</summary>
        [HttpGet]
        public async Task<ActionResult<PagedResult<StaffListItemDto>>> Get([FromQuery] StaffQuery query, CancellationToken cancellationToken) =>
            Ok(await _staff.GetStaffAsync(query, cancellationToken));

        [HttpGet("{userId:int}")]
        [ProducesResponseType<StaffDetailsDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int userId, CancellationToken cancellationToken) =>
            (await _staff.GetByIdAsync(userId, cancellationToken)).ToActionResult(this);

        /// <summary>Pending → Active. The organization must be active.</summary>
        [HttpPost("{userId:int}/approve")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Approve(int userId, CancellationToken cancellationToken) =>
            (await _staff.ApproveAsync(userId, cancellationToken)).ToActionResult(this);

        /// <summary>Pending → Rejected. A reason is required.</summary>
        [HttpPost("{userId:int}/reject")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reject(int userId, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _staff.RejectAsync(userId, request.Reason, cancellationToken)).ToActionResult(this);

        /// <summary>Active → Deactivated. A reason is required. Takes effect on the user's next request.</summary>
        [HttpPost("{userId:int}/deactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Deactivate(int userId, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _staff.DeactivateAsync(userId, request.Reason, cancellationToken)).ToActionResult(this);

        /// <summary>Deactivated → Active. The organization must be active.</summary>
        [HttpPost("{userId:int}/reactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Reactivate(int userId, CancellationToken cancellationToken) =>
            (await _staff.ReactivateAsync(userId, cancellationToken)).ToActionResult(this);
    }
}
