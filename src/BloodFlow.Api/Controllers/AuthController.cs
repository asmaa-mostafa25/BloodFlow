using BloodFlow.Api.Common;
using BloodFlow.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers
{
    [ApiController]
    [Route("api/auth")]
    [AllowAnonymous]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        /// <summary>Sign in with email and password. Only Active accounts get a token.</summary>
        /// <response code="200">Token, its expiry time, and basic user info.</response>
        /// <response code="401">Wrong email or password (same message for both).</response>
        /// <response code="403">Account Pending, Rejected, Deactivated, locked out, or organization deactivated.</response>
        [HttpPost("login")]
        [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
        {
            var result = await _authService.LoginAsync(request, cancellationToken);
            return result.ToActionResult(this);
        }

        /// <summary>
        /// A hospital or blood bank employee creates an account. It starts as Pending
        /// and cannot sign in until the System Admin approves it.
        /// </summary>
        /// <response code="201">Account created as Pending.</response>
        /// <response code="400">Invalid data, weak password, or organization missing/inactive.</response>
        /// <response code="409">Email already registered.</response>
        [HttpPost("register/staff")]
        [ProducesResponseType<RegisterStaffResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> RegisterStaff(RegisterStaffRequest request, CancellationToken cancellationToken)
        {
            var result = await _authService.RegisterStaffAsync(request, cancellationToken);
            return result.IsSuccess
                ? StatusCode(StatusCodes.Status201Created, result.Value)
                : this.ToProblem(result.Error!);
        }
    }
}
