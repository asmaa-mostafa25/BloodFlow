using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    /// <summary>
    /// Workflow A3: the System Admin can SEE stock but never change it, so the stock
    /// ledger always shows which staff member made each change.
    /// </summary>
    [ApiController]
    [Route("api/admin/inventory")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class InventoryReportsController : ControllerBase
    {
        private readonly IInventoryReportService _reports;

        public InventoryReportsController(IInventoryReportService reports)
        {
            _reports = reports;
        }

        [HttpGet("organizations/{organizationId:int}/summary")]
        [ProducesResponseType<InventorySummaryDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Summary(int organizationId, CancellationToken cancellationToken) =>
            (await _reports.GetSummaryAsync(organizationId, cancellationToken)).ToActionResult(this);
    }
}
