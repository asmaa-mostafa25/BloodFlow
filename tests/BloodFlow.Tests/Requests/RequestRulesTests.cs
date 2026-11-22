using BloodFlow.Application.Requests;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Tests.Requests;

public class RequestRulesTests
{
    private static readonly DateOnly Today = new(2026, 11, 22);
    private static readonly DateTime Now = new(2026, 11, 22, 10, 0, 0, DateTimeKind.Utc);

    // ---------- Transitions ----------

    [Theory]
    [InlineData(RequestStatus.Pending, RequestStatus.Approved)]
    [InlineData(RequestStatus.Pending, RequestStatus.Rejected)]
    [InlineData(RequestStatus.Pending, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.Approved, RequestStatus.Fulfilled)]
    [InlineData(RequestStatus.Approved, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.Fulfilled, RequestStatus.Received)]
    public void AllowedTransitions(RequestStatus from, RequestStatus to) =>
        Assert.True(RequestRules.CanTransition(from, to));

    [Theory]
    [InlineData(RequestStatus.Pending, RequestStatus.Fulfilled)]   // cannot skip the reservation
    [InlineData(RequestStatus.Pending, RequestStatus.Received)]
    [InlineData(RequestStatus.Approved, RequestStatus.Rejected)]   // after approval: cancel, not reject
    [InlineData(RequestStatus.Fulfilled, RequestStatus.Cancelled)] // blood already left
    [InlineData(RequestStatus.Received, RequestStatus.Cancelled)]
    [InlineData(RequestStatus.Rejected, RequestStatus.Approved)]
    [InlineData(RequestStatus.Cancelled, RequestStatus.Pending)]
    [InlineData(RequestStatus.Approved, RequestStatus.Approved)]
    public void ForbiddenTransitions(RequestStatus from, RequestStatus to) =>
        Assert.False(RequestRules.CanTransition(from, to));

    // ---------- FEFO allocation ----------

    [Fact]
    public void Fefo_TakesEarliestExpiryFirst()
    {
        var candidates = new[]
        {
            new BatchCandidate(1, Today.AddDays(20), 5),
            new BatchCandidate(2, Today.AddDays(3), 2),
            new BatchCandidate(3, Today.AddDays(10), 4)
        };

        var result = RequestRules.AllocateFefo(candidates, 5);

        Assert.NotNull(result);
        Assert.Equal(new[] { new Allocation(2, 2), new Allocation(3, 3) }, result);
    }

    [Fact]
    public void Fefo_SingleBatchEnough_UsesOnlyIt()
    {
        var result = RequestRules.AllocateFefo(new[] { new BatchCandidate(7, Today, 10) }, 3);
        Assert.Equal(new[] { new Allocation(7, 3) }, result);
    }

    [Fact]
    public void Fefo_NotEnoughStock_ReturnsNull_NoPartialApproval()
    {
        var candidates = new[] { new BatchCandidate(1, Today.AddDays(1), 2), new BatchCandidate(2, Today.AddDays(2), 1) };
        Assert.Null(RequestRules.AllocateFefo(candidates, 4));
    }

    [Fact]
    public void Fefo_SkipsBatchesWithNothingAvailable()
    {
        var candidates = new[] { new BatchCandidate(1, Today, 0), new BatchCandidate(2, Today.AddDays(1), -1), new BatchCandidate(3, Today.AddDays(5), 2) };
        Assert.Equal(new[] { new Allocation(3, 2) }, RequestRules.AllocateFefo(candidates, 2));
    }

    [Fact]
    public void Fefo_SameExpiry_LowerBatchIdFirst()
    {
        var candidates = new[] { new BatchCandidate(9, Today, 1), new BatchCandidate(4, Today, 1) };
        Assert.Equal(new[] { new Allocation(4, 1) }, RequestRules.AllocateFefo(candidates, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Fefo_NonPositiveUnits_ReturnsNull(int units) =>
        Assert.Null(RequestRules.AllocateFefo(new[] { new BatchCandidate(1, Today, 5) }, units));

    // ---------- Flags ----------

    [Fact]
    public void Flags_ApprovedPastNeededBy_IsOverdue()
    {
        var flags = RequestRules.ComputeFlags(RequestStatus.Approved, RequestType.Normal,
            neededByUtc: Now.AddHours(-1), createdAtUtc: Now.AddDays(-1), statusChangedAtUtc: Now.AddHours(-5), Now, 30, 48);
        Assert.True(flags.IsOverdue);
        Assert.False(flags.IsReceiptOverdue);
        Assert.False(flags.NeedsEscalation);
    }

    [Fact]
    public void Flags_FulfilledNotConfirmedInTime_IsReceiptOverdue()
    {
        var flags = RequestRules.ComputeFlags(RequestStatus.Fulfilled, RequestType.Normal,
            Now.AddDays(-3), Now.AddDays(-4), statusChangedAtUtc: Now.AddHours(-49), Now, 30, 48);
        Assert.True(flags.IsReceiptOverdue);
        Assert.False(flags.IsOverdue); // only Approved can be "overdue"
    }

    [Fact]
    public void Flags_EmergencyPendingTooLong_NeedsEscalation()
    {
        var flags = RequestRules.ComputeFlags(RequestStatus.Pending, RequestType.Emergency,
            Now.AddHours(2), createdAtUtc: Now.AddMinutes(-31), Now.AddMinutes(-31), Now, 30, 48);
        Assert.True(flags.NeedsEscalation);
    }

    [Fact]
    public void Flags_NormalPending_NeverEscalates()
    {
        var flags = RequestRules.ComputeFlags(RequestStatus.Pending, RequestType.Normal,
            Now.AddHours(2), Now.AddDays(-2), Now.AddDays(-2), Now, 30, 48);
        Assert.Equal(new RequestFlags(false, false, false), flags);
    }

    [Fact]
    public void Flags_ReceivedRequest_HasNoFlags()
    {
        var flags = RequestRules.ComputeFlags(RequestStatus.Received, RequestType.Emergency,
            Now.AddDays(-5), Now.AddDays(-6), Now.AddDays(-5), Now, 30, 48);
        Assert.Equal(new RequestFlags(false, false, false), flags);
    }

    // ---------- Verification / emergency override ----------

    [Theory]
    [InlineData(RequestType.Normal, VerificationStatus.Verified, true)]
    [InlineData(RequestType.Normal, VerificationStatus.Unverified, false)]
    [InlineData(RequestType.Emergency, VerificationStatus.Unverified, true)] // emergencies may be reserved early
    [InlineData(RequestType.Emergency, VerificationStatus.Verified, true)]
    public void CanReserve(RequestType type, VerificationStatus verification, bool expected) =>
        Assert.Equal(expected, RequestRules.CanReserve(type, verification));

    [Theory]
    [InlineData(RequestType.Normal, VerificationStatus.Verified, false, true)]
    [InlineData(RequestType.Normal, VerificationStatus.Unverified, true, false)]   // no override for normal requests
    [InlineData(RequestType.Emergency, VerificationStatus.Unverified, false, false)] // needs a reason
    [InlineData(RequestType.Emergency, VerificationStatus.Unverified, true, true)]
    [InlineData(RequestType.Emergency, VerificationStatus.Verified, false, true)]
    public void CanFulfil(RequestType type, VerificationStatus verification, bool hasReason, bool expected) =>
        Assert.Equal(expected, RequestRules.CanFulfil(type, verification, hasReason));
}
