using BloodFlow.Application.Common;

namespace BloodFlow.Api.Common
{
    /// <summary>Reads the signed-in user from the JWT claims of the current request.</summary>
    internal sealed class CurrentUser : ICurrentUser
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? UserId =>
            int.TryParse(_httpContextAccessor.HttpContext?.User.FindFirst(AppClaimTypes.UserId)?.Value, out var id)
                ? id
                : null;

        public string? Role => _httpContextAccessor.HttpContext?.User.FindFirst(AppClaimTypes.Role)?.Value;

        public string? IpAddress => _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();
    }
}
