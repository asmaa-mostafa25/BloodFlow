using System.ComponentModel.DataAnnotations;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Staff
{
    /// <summary>Filters for the admin's staff list. Default view: accounts waiting for approval.</summary>
    public sealed record StaffQuery : PageQuery
    {
        public AccountStatus? Status { get; init; }
        public int? OrganizationId { get; init; }
        public OrganizationType? StaffKind { get; init; }

        /// <summary>Matches name or email.</summary>
        public string? Search { get; init; }
    }

    public sealed record StaffListItemDto(
        int UserId,
        string FullName,
        string Email,
        string JobTitle,
        OrganizationType StaffKind,
        int OrganizationId,
        string OrganizationName,
        string GovernorateName,
        AccountStatus AccountStatus,
        DateTime CreatedAt);

    public sealed record StaffDetailsDto(
        int UserId,
        string FullName,
        string Email,
        string JobTitle,
        OrganizationType StaffKind,
        int OrganizationId,
        string OrganizationName,
        OrganizationStatus OrganizationStatus,
        string GovernorateName,
        AccountStatus AccountStatus,
        DateTime CreatedAt,
        int? ApprovedByUserId,
        DateTime? ApprovedAt,
        string? RejectionReason);

    /// <summary>Body for actions that must explain why (reject, deactivate).</summary>
    public sealed record ReasonRequest
    {
        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; init; } = string.Empty;
    }
}
