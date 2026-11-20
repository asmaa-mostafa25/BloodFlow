using System.Text;
using BloodFlow.Application.Audit;
using BloodFlow.Application.Auth;
using BloodFlow.Application.Common;
using BloodFlow.Application.Lookups;
using BloodFlow.Application.Organizations;
using BloodFlow.Application.Settings;
using BloodFlow.Application.Staff;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Authentication;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Identity;
using BloodFlow.Infrastructure.Persistence;
using BloodFlow.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace BloodFlow.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // ---------- Database ----------
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"));
            });

            // ---------- Identity ----------
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;

                // Brute-force protection: 5 wrong passwords → locked for 15 minutes.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<AppDbContext>();

            // ---------- Application services ----------
            services.AddScoped<AuditLogger>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IStaffAdminService, StaffAdminService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<ILookupService, LookupService>();
            services.AddScoped<ISystemSettingService, SystemSettingService>();
            services.AddScoped<IAuditLogService, AuditLogService>();

            // ---------- JWT ----------
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.AddScoped<JwtTokenGenerator>();

            var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>();
            if (jwtSettings is null || string.IsNullOrWhiteSpace(jwtSettings.Key))
            {
                throw new InvalidOperationException(
                    "Jwt settings are missing. Set 'Jwt' in appsettings.json and 'Jwt:Key' in user secrets.");
            }

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Keep the short claim names from the token (sub, role, ...) exactly as they are,
                    // instead of letting the handler rename them to long Microsoft URIs.
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidAudience = jwtSettings.Audience,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Key)),

                        // [Authorize(Roles = ...)] reads roles from this claim.
                        RoleClaimType = AppClaimTypes.Role,
                        NameClaimType = AppClaimTypes.Name,

                        // Default is 5 minutes of tolerance after expiry; keep it small.
                        ClockSkew = TimeSpan.FromSeconds(30)
                    };

                    // A token stays valid for up to 60 minutes. Without this check, a staff member
                    // deactivated by the admin could keep working until the token expires.
                    // One small primary-key query per request closes that gap.
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = async context =>
                        {
                            var userIdClaim = context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value;
                            if (!int.TryParse(userIdClaim, out var userId))
                            {
                                context.Fail("Invalid token subject.");
                                return;
                            }

                            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                            var state = await db.Users
                                .AsNoTracking()
                                .Where(u => u.Id == userId)
                                .Select(u => new
                                {
                                    u.AccountStatus,
                                    OrganizationStatus = db.StaffProfiles
                                        .Where(s => s.UserId == u.Id)
                                        .Select(s => (OrganizationStatus?)s.Organization.Status)
                                        .FirstOrDefault()
                                })
                                .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                            if (state is null || state.AccountStatus != AccountStatus.Active)
                            {
                                context.Fail("Account is not active.");
                            }
                            else if (state.OrganizationStatus is not null and not OrganizationStatus.Active)
                            {
                                context.Fail("Organization is not active.");
                            }
                        }
                    };
                });

            return services;
        }
    }
}
