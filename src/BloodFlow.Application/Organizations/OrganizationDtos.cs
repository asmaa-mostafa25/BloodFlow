using System.ComponentModel.DataAnnotations;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Organizations
{
    public sealed record OrganizationQuery : PageQuery
    {
        public OrganizationType? Type { get; init; }
        public OrganizationStatus? Status { get; init; }
        public short? GovernorateId { get; init; }
        public string? Search { get; init; }
    }

    /// <summary>
    /// RowVersion is sent to the client as Base64 and must come back unchanged on update,
    /// so two admins editing the same organization cannot overwrite each other.
    /// </summary>
    public sealed record OrganizationDto(
        int Id,
        OrganizationType Type,
        string Name,
        string CityName,
        short GovernorateId,
        string GovernorateName,
        OrganizationStatus Status,
        int ActiveStaffCount,
        string RowVersion);

    public sealed record CreateOrganizationRequest
    {
        [Required, EnumDataType(typeof(OrganizationType))]
        public OrganizationType? Type { get; init; }

        [Required, StringLength(200, MinimumLength = 3)]
        public string Name { get; init; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 2)]
        public string CityName { get; init; } = string.Empty;

        [Range(1, short.MaxValue)]
        public short GovernorateId { get; init; }
    }

    /// <summary>The Type cannot change after creation (staff are linked to it).</summary>
    public sealed record UpdateOrganizationRequest
    {
        [Required, StringLength(200, MinimumLength = 3)]
        public string Name { get; init; } = string.Empty;

        [Required, StringLength(100, MinimumLength = 2)]
        public string CityName { get; init; } = string.Empty;

        [Range(1, short.MaxValue)]
        public short GovernorateId { get; init; }

        [Required]
        public string RowVersion { get; init; } = string.Empty;
    }
}
