using BloodFlow.Application.Common;

namespace BloodFlow.Application.Organizations
{
    /// <summary>System Admin management of hospitals and blood banks (workflow A2).</summary>
    public interface IOrganizationService
    {
        Task<PagedResult<OrganizationDto>> GetAsync(OrganizationQuery query, CancellationToken cancellationToken = default);
        Task<Result<OrganizationDto>> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<OrganizationDto>> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default);
        Task<Result<OrganizationDto>> UpdateAsync(int id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default);
        Task<Result> DeactivateAsync(int id, string reason, CancellationToken cancellationToken = default);
        Task<Result> ActivateAsync(int id, CancellationToken cancellationToken = default);
    }
}
