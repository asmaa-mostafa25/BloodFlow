using BloodFlow.Application.Inventory;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Tests.Inventory;

public class InventoryRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);

    // ---------- Intake ----------

    [Fact]
    public void Intake_WithEnteredExpiry_IsValid()
    {
        var error = InventoryRules.ValidateIntake(5, Today.AddDays(-2), Today.AddDays(30), Today, 35, out var expiry, out var basis);

        Assert.Null(error);
        Assert.Equal(Today.AddDays(30), expiry);
        Assert.Equal(ExpiryBasis.Entered, basis);
    }

    [Fact]
    public void Intake_WithoutExpiry_UsesDefaultShelfLife()
    {
        var collected = Today.AddDays(-1);

        var error = InventoryRules.ValidateIntake(5, collected, null, Today, 35, out var expiry, out var basis);

        Assert.Null(error);
        Assert.Equal(collected.AddDays(35), expiry);
        Assert.Equal(ExpiryBasis.DefaultShelfLife, basis);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Intake_NonPositiveQuantity_IsRejected(int quantity)
    {
        var error = InventoryRules.ValidateIntake(quantity, Today, Today.AddDays(10), Today, 35, out _, out _);
        Assert.Equal("Inventory.InvalidQuantity", error?.Code);
    }

    [Fact]
    public void Intake_CollectionInFuture_IsRejected()
    {
        var error = InventoryRules.ValidateIntake(1, Today.AddDays(1), Today.AddDays(10), Today, 35, out _, out _);
        Assert.Equal("Inventory.CollectionInFuture", error?.Code);
    }

    [Fact]
    public void Intake_ExpiryNotAfterCollection_IsRejected()
    {
        var error = InventoryRules.ValidateIntake(1, Today, Today, Today, 35, out _, out _);
        Assert.Equal("Inventory.ExpiryBeforeCollection", error?.Code);
    }

    [Theory]
    [InlineData(0)]   // expires today → not "in the future"
    [InlineData(-1)]  // already past
    public void Intake_ExpiryNotInFuture_IsRejected(int expiryOffset)
    {
        var error = InventoryRules.ValidateIntake(1, Today.AddDays(-40), Today.AddDays(expiryOffset), Today, 35, out _, out _);
        Assert.Equal("Inventory.AlreadyExpired", error?.Code);
    }

    [Fact]
    public void Intake_OldCollectionWithDefaultShelfLife_IsRejectedAsExpired()
    {
        var error = InventoryRules.ValidateIntake(1, Today.AddDays(-40), null, Today, 35, out _, out _);
        Assert.Equal("Inventory.AlreadyExpired", error?.Code);
    }

    // ---------- Adjust ----------

    [Theory]
    [InlineData(AdjustmentReason.Damaged, -2)]
    [InlineData(AdjustmentReason.Expired, -10)]
    [InlineData(AdjustmentReason.Correction, -1)]
    [InlineData(AdjustmentReason.Correction, 3)]
    public void Adjust_ValidCases_Pass(AdjustmentReason reason, int delta)
    {
        Assert.Null(InventoryRules.ValidateAdjustment(BatchStatus.Active, 10, 0, delta, reason));
    }

    [Fact]
    public void Adjust_Zero_IsRejected()
    {
        Assert.Equal("Inventory.ZeroAdjustment",
            InventoryRules.ValidateAdjustment(BatchStatus.Active, 10, 0, 0, AdjustmentReason.Correction)?.Code);
    }

    [Theory]
    [InlineData(AdjustmentReason.Damaged)]
    [InlineData(AdjustmentReason.Expired)]
    public void Adjust_DamagedOrExpired_CannotIncrease(AdjustmentReason reason)
    {
        Assert.Equal("Inventory.ReasonRequiresDecrease",
            InventoryRules.ValidateAdjustment(BatchStatus.Active, 10, 0, 2, reason)?.Code);
    }

    [Fact]
    public void Adjust_ExpiredBatch_CannotIncrease()
    {
        Assert.Equal("Inventory.ExpiredBatchIncrease",
            InventoryRules.ValidateAdjustment(BatchStatus.Expired, 10, 0, 2, AdjustmentReason.Correction)?.Code);
    }

    [Fact]
    public void Adjust_ExpiredBatch_CanBeDisposed()
    {
        Assert.Null(InventoryRules.ValidateAdjustment(BatchStatus.Expired, 10, 0, -10, AdjustmentReason.Expired));
    }

    [Fact]
    public void Adjust_BelowZero_IsRejected()
    {
        Assert.Equal("Inventory.NegativeStock",
            InventoryRules.ValidateAdjustment(BatchStatus.Active, 3, 0, -4, AdjustmentReason.Damaged)?.Code);
    }

    [Fact]
    public void Adjust_BelowReserved_IsConflict()
    {
        var error = InventoryRules.ValidateAdjustment(BatchStatus.Active, 10, 6, -5, AdjustmentReason.Damaged);

        Assert.Equal("Inventory.BelowReserved", error?.Code);
        Assert.Equal(BloodFlow.Application.Common.ErrorType.Conflict, error?.Type);
    }

    // ---------- Expiry ----------

    [Theory]
    [InlineData(1, false)]  // expires tomorrow
    [InlineData(0, false)]  // expires today: still usable until midnight
    [InlineData(-1, true)]  // expired yesterday
    public void IsExpired_UsesEndOfExpiryDay(int offset, bool expected)
    {
        Assert.Equal(expected, InventoryRules.IsExpired(Today.AddDays(offset), Today));
    }
}
