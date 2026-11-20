using BloodFlow.Application.Common;

namespace BloodFlow.Application.Inventory
{
    /// <summary>
    /// Stock operations for hospital and blood bank staff (workflows B1 and H4).
    /// Always works on the caller's own organization (from the token), never on an id sent by the client.
    /// </summary>
    public interface IInventoryService
    {
        Task<Result<BatchDto>> IntakeAsync(IntakeBatchRequest request, CancellationToken cancellationToken = default);
        Task<Result<BatchDto>> AdjustAsync(int batchId, AdjustBatchRequest request, CancellationToken cancellationToken = default);

        Task<Result<PagedResult<BatchDto>>> GetBatchesAsync(BatchQuery query, CancellationToken cancellationToken = default);
        Task<Result<BatchDto>> GetBatchAsync(int batchId, CancellationToken cancellationToken = default);
        Task<Result<PagedResult<InventoryTransactionDto>>> GetLedgerAsync(LedgerQuery query, CancellationToken cancellationToken = default);
        Task<Result<InventorySummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>Read-only stock view for the System Admin (workflow A3: the admin never edits stock).</summary>
    public interface IInventoryReportService
    {
        Task<Result<InventorySummaryDto>> GetSummaryAsync(int organizationId, CancellationToken cancellationToken = default);
    }

    /// <summary>Marks batches past their expiry date as Expired. Run daily by a background job.</summary>
    public interface IBatchExpiryService
    {
        /// <returns>How many batches were marked Expired.</returns>
        Task<int> ExpireDueBatchesAsync(CancellationToken cancellationToken = default);
    }
}
