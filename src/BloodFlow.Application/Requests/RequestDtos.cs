using System.ComponentModel.DataAnnotations;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Requests
{
    /// <summary>Hospital staff create a request for their own hospital (workflow H1 / section 3 step 1).</summary>
    public sealed record CreateBloodRequestRequest
    {
        [Required, EnumDataType(typeof(RequestType))]
        public RequestType? RequestType { get; init; }

        [Range(1, 8)]
        public byte BloodGroupId { get; init; }

        /// <summary>The upper limit comes from the Requests.MaxUnitsPerRequest setting.</summary>
        [Range(1, 100)]
        public int UnitsRequested { get; init; }

        /// <summary>UTC. Must be in the future.</summary>
        [Required]
        public DateTime? NeededBy { get; init; }

        [Required, StringLength(200, MinimumLength = 2)]
        public string PatientFullName { get; init; } = string.Empty;

        public Guid? ClientRequestId { get; init; }
    }

    public sealed record FulfilRequestRequest
    {
        /// <summary>
        /// Only for an UNVERIFIED EMERGENCY: why the blood must leave before verification.
        /// Recorded as an emergency override and flagged for admin review.
        /// </summary>
        [StringLength(500, MinimumLength = 10)]
        public string? EmergencyOverrideReason { get; init; }
    }

    public sealed record ReviewOverrideRequest
    {
        [StringLength(500)]
        public string? Notes { get; init; }
    }

    public sealed record RequestQuery : PageQuery
    {
        public RequestStatus? Status { get; init; }
        public RequestType? Type { get; init; }
        public byte? BloodGroupId { get; init; }

        /// <summary>Only requests that are overdue, waiting too long for receipt, or need escalation.</summary>
        public bool OnlyFlagged { get; init; }
    }

    public sealed record BoardQuery : PageQuery
    {
        public RequestType? Type { get; init; }
        public byte? BloodGroupId { get; init; }

        /// <summary>By default the board shows requests in your organization's governorate only.</summary>
        public bool AllGovernorates { get; init; }
    }

    public sealed record OverrideQuery : PageQuery
    {
        public OverrideReviewStatus? ReviewStatus { get; init; } = OverrideReviewStatus.PendingReview;
    }

    public sealed record RequestSummaryDto(
        int Id,
        string RequestNumber,
        RequestType RequestType,
        RequestStatus Status,
        VerificationStatus VerificationStatus,
        byte BloodGroupId,
        string BloodGroupCode,
        int UnitsRequested,
        DateTime NeededBy,
        int DestinationHospitalId,
        string DestinationHospitalName,
        string GovernorateName,
        int? ExecutingOrganizationId,
        string? ExecutingOrganizationName,
        DateTime CreatedAt,
        DateTime StatusChangedAt,
        RequestFlags Flags,
        // Board only: how many usable units of this blood group YOUR organization has now.
        int? AvailableAtYourOrganization);

    public sealed record ReservationLineDto(
        int Id, int BatchId, DateOnly BatchExpiryDate, int Quantity, ReservationLineStatus Status, string? InvalidationReason);

    public sealed record ReservationDto(
        int Id,
        int OrganizationId,
        string OrganizationName,
        ReservationStatus Status,
        int ActiveUnits,
        bool NeedsReallocation,
        DateTime CreatedAt,
        DateTime? ClosedAt,
        IReadOnlyList<ReservationLineDto> Lines);

    public sealed record RequestDetailsDto(
        RequestSummaryDto Summary,
        RequesterCapacity RequesterCapacity,
        int CreatedByUserId,
        // Only shown to the destination hospital and the requester (patient privacy).
        // Null for everyone else, and after anonymization.
        string? PatientFullName,
        bool PatientAnonymized,
        int? VerifiedByUserId,
        DateTime? VerifiedAt,
        bool EmergencyOverrideUsed,
        ReservationDto? Reservation);

    public sealed record StatusHistoryDto(
        RequestStatus? FromStatus, RequestStatus ToStatus, int ChangedByUserId, string? Reason, DateTime ChangedAt);

    public sealed record EmergencyOverrideDto(
        int Id,
        int RequestId,
        string RequestNumber,
        int OrganizationId,
        string OrganizationName,
        int OverriddenByUserId,
        string ReasonText,
        DateTime OverriddenAt,
        OverrideReviewStatus ReviewStatus,
        int? ReviewedByUserId,
        DateTime? ReviewedAt,
        string? ReviewNotes);
}
