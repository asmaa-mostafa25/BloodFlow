using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Lookups
{
    /// <summary>Public reference data for frontend dropdowns. No login needed.</summary>
    public interface ILookupService
    {
        Task<IReadOnlyList<GovernorateDto>> GetGovernoratesAsync(CancellationToken cancellationToken = default);
        Task<IReadOnlyList<BloodGroupDto>> GetBloodGroupsAsync(CancellationToken cancellationToken = default);

        /// <summary>Only Active organizations are returned.</summary>
        Task<IReadOnlyList<OrganizationLookupDto>> GetActiveOrganizationsAsync(
            OrganizationType? type, short? governorateId, CancellationToken cancellationToken = default);
    }
}
