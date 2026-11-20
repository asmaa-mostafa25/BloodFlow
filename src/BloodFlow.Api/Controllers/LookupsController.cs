using BloodFlow.Application.Lookups;
using BloodFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers
{
    /// <summary>Reference data for dropdowns. Public: the registration form needs it before login.</summary>
    [ApiController]
    [Route("api/lookups")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class LookupsController : ControllerBase
    {
        private readonly ILookupService _lookups;

        public LookupsController(ILookupService lookups)
        {
            _lookups = lookups;
        }

        [HttpGet("governorates")]
        public async Task<ActionResult<IReadOnlyList<GovernorateDto>>> Governorates(CancellationToken cancellationToken) =>
            Ok(await _lookups.GetGovernoratesAsync(cancellationToken));

        [HttpGet("blood-groups")]
        public async Task<ActionResult<IReadOnlyList<BloodGroupDto>>> BloodGroups(CancellationToken cancellationToken) =>
            Ok(await _lookups.GetBloodGroupsAsync(cancellationToken));

        /// <summary>Active hospitals and blood banks, optionally filtered by type and governorate.</summary>
        [HttpGet("organizations")]
        public async Task<ActionResult<IReadOnlyList<OrganizationLookupDto>>> Organizations(
            [FromQuery] OrganizationType? type, [FromQuery] short? governorateId, CancellationToken cancellationToken) =>
            Ok(await _lookups.GetActiveOrganizationsAsync(type, governorateId, cancellationToken));
    }
}
