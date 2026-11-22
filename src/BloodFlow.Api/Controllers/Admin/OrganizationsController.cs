using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Organizations;
using BloodFlow.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    /// <summary>Workflow A2: the System Admin creates, edits and deactivates hospitals and blood banks.</summary>
    [ApiController]
    [Route("api/admin/organizations")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class OrganizationsController : ControllerBase
    {
        private readonly IOrganizationService _organizations;

        public OrganizationsController(IOrganizationService organizations)
        {
            _organizations = organizations;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<OrganizationDto>>> Get([FromQuery] OrganizationQuery query, CancellationToken cancellationToken) =>
            Ok(await _organizations.GetAsync(query, cancellationToken));

        [HttpGet("{id:int}")]
        [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken) =>
            (await _organizations.GetByIdAsync(id, cancellationToken)).ToActionResult(this);

        [HttpPost]
        [ProducesResponseType<OrganizationDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Create(CreateOrganizationRequest request, CancellationToken cancellationToken)
        {
            var result = await _organizations.CreateAsync(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value)
                : this.ToProblem(result.Error!);
        }

        /// <summary>
        /// Update name, city and governorate. Send back the rowVersion you received;
        /// if someone changed the organization meanwhile you get 409.
        /// </summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType<OrganizationDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(int id, UpdateOrganizationRequest request, CancellationToken cancellationToken) =>
            (await _organizations.UpdateAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>Its staff can no longer sign in or use the API until it is activated again.</summary>
        [HttpPost("{id:int}/deactivate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Deactivate(int id, ReasonRequest request, CancellationToken cancellationToken) =>
            (await _organizations.DeactivateAsync(id, request.Reason, cancellationToken)).ToActionResult(this);

        [HttpPost("{id:int}/activate")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Activate(int id, CancellationToken cancellationToken) =>
            (await _organizations.ActivateAsync(id, cancellationToken)).ToActionResult(this);
    }
}
