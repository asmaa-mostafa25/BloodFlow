using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BloodFlow.Application.Common;
using BloodFlow.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BloodFlow.Infrastructure.Authentication
{
    internal sealed record JwtToken(string Value, DateTime ExpiresAtUtc);

    internal class JwtTokenGenerator
    {
        private readonly JwtSettings _jwtSettings;

        public JwtTokenGenerator(IOptions<JwtSettings> jwtSettings)
        {
            _jwtSettings = jwtSettings.Value;
        }

        public JwtToken GenerateToken(ApplicationUser user, IList<string> roles, int? organizationId)
        {
            // Short, standard claim names (sub, email, name, role) instead of the long
            // "http://schemas..." URIs, so the frontend can read the token easily.
            var claims = new List<Claim>
            {
                new(AppClaimTypes.UserId, user.Id.ToString()),
                new(AppClaimTypes.Email, user.Email!),
                new(AppClaimTypes.Name, user.FullName),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(AppClaimTypes.Role, role));
            }

            if (organizationId.HasValue)
            {
                claims.Add(new Claim(AppClaimTypes.OrganizationId, organizationId.Value.ToString()));
            }

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Key));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var expiresAtUtc = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expiresAtUtc,
                signingCredentials: creds);

            return new JwtToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
        }
    }
}
