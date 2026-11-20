using BloodFlow.Application.Common;

namespace BloodFlow.Application.Auth
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

        Task<Result<RegisterStaffResponse>> RegisterStaffAsync(RegisterStaffRequest request, CancellationToken cancellationToken = default);
    }
}
