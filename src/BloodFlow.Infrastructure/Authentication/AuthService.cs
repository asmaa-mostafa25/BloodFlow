using BloodFlow.Application.Auth;
using BloodFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace BloodFlow.Infrastructure.Authentication
{
    internal class AuthService:IAuthService
    {
       
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtTokenGenerator _jwtTokenGenerator;

        public  AuthService(UserManager<ApplicationUser> userManager, JwtTokenGenerator jwtTokenGenerator)
        {
            _userManager = userManager;
            _jwtTokenGenerator = jwtTokenGenerator;
        }
        public async Task<AuthResponse?> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                return null;
            }
            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid)
            {
                return null;
            }
            var roles = await _userManager.GetRolesAsync(user);
            
           
            return _jwtTokenGenerator.GenerateToken(user, roles);

        }

    }
}
