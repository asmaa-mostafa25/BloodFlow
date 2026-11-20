using BloodFlow.Application.Common;

namespace BloodFlow.Application.Staff
{
    /// <summary>System Admin actions on staff accounts (workflow A1).</summary>
    public interface IStaffAdminService
    {
        Task<PagedResult<StaffListItemDto>> GetStaffAsync(StaffQuery query, CancellationToken cancellationToken = default);
        Task<Result<StaffDetailsDto>> GetByIdAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Pending → Active. The organization must be Active.</summary>
        Task<Result> ApproveAsync(int userId, CancellationToken cancellationToken = default);

        /// <summary>Pending → Rejected, with a reason.</summary>
        Task<Result> RejectAsync(int userId, string reason, CancellationToken cancellationToken = default);

        /// <summary>Active → Deactivated, with a reason.</summary>
        Task<Result> DeactivateAsync(int userId, string reason, CancellationToken cancellationToken = default);

        /// <summary>Deactivated → Active. The organization must be Active.</summary>
        Task<Result> ReactivateAsync(int userId, CancellationToken cancellationToken = default);
    }
}
