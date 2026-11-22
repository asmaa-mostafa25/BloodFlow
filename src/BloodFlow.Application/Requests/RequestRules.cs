using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Requests
{
    /// <summary>A usable batch the allocator may take units from.</summary>
    public sealed record BatchCandidate(int BatchId, DateOnly ExpiryDate, int Available);

    /// <summary>"Take Quantity units from BatchId."</summary>
    public sealed record Allocation(int BatchId, int Quantity);

    /// <summary>Monitoring flags shown to staff and to the admin (workflow A3).</summary>
    public sealed record RequestFlags(bool IsOverdue, bool IsReceiptOverdue, bool NeedsEscalation);

    /// <summary>
    /// The request lifecycle rules (Phase 1 sections 3 and 4) as pure functions:
    /// no database, easy to read and unit test.
    /// </summary>
    public static class RequestRules
    {
        /// <summary>Allowed status changes. Everything else is refused (and also blocked by a CHECK in the database).</summary>
        public static bool CanTransition(RequestStatus from, RequestStatus to) => (from, to) switch
        {
            (RequestStatus.Pending, RequestStatus.Approved) => true,
            (RequestStatus.Pending, RequestStatus.Rejected) => true,
            (RequestStatus.Pending, RequestStatus.Cancelled) => true,
            (RequestStatus.Approved, RequestStatus.Fulfilled) => true,
            (RequestStatus.Approved, RequestStatus.Cancelled) => true,
            (RequestStatus.Fulfilled, RequestStatus.Received) => true,
            _ => false
        };

        /// <summary>
        /// FEFO (First-Expiring First-Out): take units from the batch that expires first,
        /// then the next, until the request is covered.
        /// Returns null when there is not enough stock: there are no partial approvals.
        /// </summary>
        public static IReadOnlyList<Allocation>? AllocateFefo(IEnumerable<BatchCandidate> candidates, int units)
        {
            if (units <= 0) return null;

            var result = new List<Allocation>();
            var remaining = units;

            foreach (var batch in candidates.Where(c => c.Available > 0).OrderBy(c => c.ExpiryDate).ThenBy(c => c.BatchId))
            {
                var take = Math.Min(batch.Available, remaining);
                result.Add(new Allocation(batch.BatchId, take));
                remaining -= take;
                if (remaining == 0) return result;
            }

            return null;
        }

        /// <summary>
        /// Overdue: approved but not fulfilled by "needed by".
        /// Receipt overdue: fulfilled but not confirmed within N hours.
        /// Needs escalation: an emergency still pending after N minutes.
        /// </summary>
        public static RequestFlags ComputeFlags(
            RequestStatus status,
            RequestType type,
            DateTime neededByUtc,
            DateTime createdAtUtc,
            DateTime statusChangedAtUtc,
            DateTime nowUtc,
            int escalationMinutes,
            int receiptConfirmationHours) => new(
                IsOverdue: status == RequestStatus.Approved && nowUtc > neededByUtc,
                IsReceiptOverdue: status == RequestStatus.Fulfilled && nowUtc > statusChangedAtUtc.AddHours(receiptConfirmationHours),
                NeedsEscalation: type == RequestType.Emergency && status == RequestStatus.Pending
                                 && nowUtc > createdAtUtc.AddMinutes(escalationMinutes));

        /// <summary>
        /// Can a blood bank approve and reserve now? Normal requests must be verified first;
        /// emergencies may be reserved early, even unverified (section 4).
        /// </summary>
        public static bool CanReserve(RequestType type, VerificationStatus verification) =>
            verification == VerificationStatus.Verified || type == RequestType.Emergency;

        /// <summary>
        /// Can it be fulfilled? Verified always; an unverified emergency only with an override reason.
        /// </summary>
        public static bool CanFulfil(RequestType type, VerificationStatus verification, bool hasOverrideReason) =>
            verification == VerificationStatus.Verified
            || (type == RequestType.Emergency && hasOverrideReason);
    }
}
