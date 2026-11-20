using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers
{
    /// <summary>
    /// Workflows B1 and H4: hospital and blood bank staff manage the stock of THEIR OWN organization.
    /// The organization always comes from the token (org_id), never from the request.
    /// </summary>
    [ApiController]
    [Route("api/inventory")]
    [Authorize(Roles = Roles.Staff)]
    [Produces("application/json")]
    public class InventoryController : ControllerBase
    {
        private readonly IInventoryService _inventory;

        public InventoryController(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        /// <summary>Stock per blood group: on hand, reserved, available, near expiry, expired.</summary>
        [HttpGet("summary")]
        [ProducesResponseType<InventorySummaryDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Summary(CancellationToken cancellationToken) =>
            (await _inventory.GetSummaryAsync(cancellationToken)).ToActionResult(this);

        /// <summary>Batches in FEFO order (earliest expiry first). Use expiringWithinDays for near-expiry lists.</summary>
        [HttpGet("batches")]
        [ProducesResponseType<PagedResult<BatchDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> GetBatches([FromQuery] BatchQuery query, CancellationToken cancellationToken) =>
            (await _inventory.GetBatchesAsync(query, cancellationToken)).ToActionResult(this);

        [HttpGet("batches/{id:int}")]
        [ProducesResponseType<BatchDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetBatch(int id, CancellationToken cancellationToken) =>
            (await _inventory.GetBatchAsync(id, cancellationToken)).ToActionResult(this);

        /// <summary>
        /// Record new units (manual intake). If expiryDate is empty it is collection date + the
        /// default shelf life setting. Send a new clientRequestId per submission to make retries safe.
        /// </summary>
        [HttpPost("batches")]
        [ProducesResponseType<BatchDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Intake(IntakeBatchRequest request, CancellationToken cancellationToken)
        {
            var result = await _inventory.IntakeAsync(request, cancellationToken);
            return result.IsSuccess
                ? CreatedAtAction(nameof(GetBatch), new { id = result.Value.Id }, result.Value)
                : this.ToProblem(result.Error!);
        }

        /// <summary>
        /// Change a batch quantity by hand: Damaged or Expired (decrease only) or Correction (±).
        /// A reason is required. On-hand can never go below reserved.
        /// </summary>
        [HttpPost("batches/{id:int}/adjust")]
        [ProducesResponseType<BatchDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Adjust(int id, AdjustBatchRequest request, CancellationToken cancellationToken) =>
            (await _inventory.AdjustAsync(id, request, cancellationToken)).ToActionResult(this);

        /// <summary>The stock ledger: every change to every batch, newest first.</summary>
        [HttpGet("transactions")]
        [ProducesResponseType<PagedResult<InventoryTransactionDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Ledger([FromQuery] LedgerQuery query, CancellationToken cancellationToken) =>
            (await _inventory.GetLedgerAsync(query, cancellationToken)).ToActionResult(this);
    }
}
