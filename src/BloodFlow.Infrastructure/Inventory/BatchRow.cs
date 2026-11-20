using System.Linq.Expressions;
using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Infrastructure.Inventory
{
    /// <summary>
    /// What we read from SQL for one batch. Values that depend on "today"
    /// (days to expiry, effective status, available) are calculated in C# afterwards.
    /// </summary>
    internal sealed record BatchRow(
        int Id, byte BloodGroupId, string BloodGroupCode, BatchSourceType SourceType,
        DateOnly CollectionDate, DateOnly ExpiryDate, ExpiryBasis ExpiryBasis,
        int InitialQuantity, int OnHandQuantity, int ReservedQuantity, BatchStatus Status, DateTime CreatedAt)
    {
        public static readonly Expression<Func<BloodBatch, BatchRow>> Projection = b => new BatchRow(
            b.Id, b.BloodGroupId, b.BloodGroup.Code, b.SourceType,
            b.OriginalCollectionDate, b.ExpiryDate, b.ExpiryBasis,
            b.InitialQuantity, b.OnHandQuantity, b.ReservedQuantity, b.Status, b.CreatedAt);

        public BatchDto ToDto(DateOnly today)
        {
            var status = Status == BatchStatus.Active && InventoryRules.IsExpired(ExpiryDate, today)
                ? BatchStatus.Expired
                : Status;
            var available = status == BatchStatus.Active ? OnHandQuantity - ReservedQuantity : 0;

            return new BatchDto(
                Id, BloodGroupId, BloodGroupCode, SourceType, CollectionDate, ExpiryDate, ExpiryBasis,
                ExpiryDate.DayNumber - today.DayNumber,
                InitialQuantity, OnHandQuantity, ReservedQuantity, available, status, CreatedAt);
        }
    }
}
