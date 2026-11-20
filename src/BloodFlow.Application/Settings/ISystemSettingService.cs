using BloodFlow.Application.Common;

namespace BloodFlow.Application.Settings
{
    public interface ISystemSettingService
    {
        Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Result<SystemSettingDto>> GetAsync(string key, CancellationToken cancellationToken = default);
        Task<Result<SystemSettingDto>> UpdateAsync(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken = default);
    }
}
