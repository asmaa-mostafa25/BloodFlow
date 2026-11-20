using BloodFlow.Application.Auth;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Identity;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Authentication
{
    internal class AuthService : IAuthService
    {
        private static readonly Error InvalidCredentials =
            Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password.");

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly JwtTokenGenerator _jwtTokenGenerator;
        private readonly AppDbContext _db;
        private readonly AuditLogger _audit;

        public AuthService(
            UserManager<ApplicationUser> userManager,
            JwtTokenGenerator jwtTokenGenerator,
            AppDbContext db,
            AuditLogger audit)
        {
            _userManager = userManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _db = db;
            _audit = audit;
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            var user = await _userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null)
            {
                return InvalidCredentials;
            }

            // Brute-force protection: after 5 wrong passwords the account is locked for 15 minutes.
            if (await _userManager.IsLockedOutAsync(user))
            {
                return Error.Forbidden("Auth.LockedOut", "Too many failed attempts. Try again later.");
            }

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                await _userManager.AccessFailedAsync(user);
                return InvalidCredentials;
            }

            await _userManager.ResetAccessFailedCountAsync(user);

            // The password is correct, so it is safe to tell the user WHY they cannot sign in.
            var statusError = user.AccountStatus switch
            {
                AccountStatus.Pending => Error.Forbidden("Auth.AccountPending",
                    "Your account is waiting for approval by the System Admin."),
                AccountStatus.Rejected => Error.Forbidden("Auth.AccountRejected",
                    "Your registration was rejected. Contact the System Admin for details."),
                AccountStatus.Deactivated => Error.Forbidden("Auth.AccountDeactivated",
                    "Your account has been deactivated."),
                _ => null
            };
            if (statusError is not null)
            {
                return statusError;
            }

            var roles = await _userManager.GetRolesAsync(user);

            // Staff can only work while their organization is active.
            int? organizationId = null;
            if (roles.Contains(Roles.HospitalStaff) || roles.Contains(Roles.BloodBankStaff))
            {
                var profile = await _db.StaffProfiles
                    .AsNoTracking()
                    .Where(s => s.UserId == user.Id)
                    .Select(s => new { s.OrganizationId, OrganizationStatus = s.Organization.Status })
                    .FirstOrDefaultAsync(cancellationToken);

                if (profile is null)
                {
                    return Error.Forbidden("Auth.StaffProfileMissing",
                        "Your staff profile is missing. Contact the System Admin.");
                }

                if (profile.OrganizationStatus != OrganizationStatus.Active)
                {
                    return Error.Forbidden("Auth.OrganizationDeactivated",
                        "Your organization has been deactivated.");
                }

                organizationId = profile.OrganizationId;
            }

            var token = _jwtTokenGenerator.GenerateToken(user, roles, organizationId);

            // One role per user in BloodFlow.
            var userDto = new AuthUserDto(user.Id, user.FullName, user.Email!, roles.FirstOrDefault() ?? string.Empty, organizationId);
            return new AuthResponse(token.Value, token.ExpiresAtUtc, userDto);
        }

        public async Task<Result<RegisterStaffResponse>> RegisterStaffAsync(
            RegisterStaffRequest request, CancellationToken cancellationToken = default)
        {
            var email = request.Email.Trim();

            var organization = await _db.Organizations
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == request.OrganizationId, cancellationToken);

            if (organization is null)
            {
                return Error.Validation("Registration.OrganizationNotFound", "The selected organization does not exist.");
            }

            if (organization.Status != OrganizationStatus.Active)
            {
                return Error.Validation("Registration.OrganizationInactive", "The selected organization is not active.");
            }

            if (await _userManager.FindByEmailAsync(email) is not null)
            {
                return Error.Conflict("Registration.EmailTaken", "An account with this email already exists.");
            }

            // The staff kind always follows the organization type; the client cannot choose it.
            var role = organization.Type == OrganizationType.Hospital ? Roles.HospitalStaff : Roles.BloodBankStaff;

            // Identity saves the user and the role in separate SaveChanges calls.
            // One transaction makes "user + role + profile + audit" all-or-nothing.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = request.FullName.Trim(),
                AccountStatus = AccountStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                return ToRegistrationError(createResult);
            }

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                // The roles are seeded at startup, so this is a server problem, not the user's.
                throw new InvalidOperationException(
                    $"Failed to add role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }

            _db.StaffProfiles.Add(new StaffProfile
            {
                UserId = user.Id,
                OrganizationId = organization.Id,
                StaffKind = organization.Type,
                JobTitle = request.JobTitle.Trim()
            });

            _audit.Add(
                AuditActions.StaffRegistered,
                AuditEntityTypes.User,
                user.Id.ToString(),
                newValues: new { user.FullName, user.Email, OrganizationId = organization.Id, StaffKind = organization.Type, JobTitle = request.JobTitle.Trim(), user.AccountStatus },
                organizationId: organization.Id,
                actorUserId: user.Id,
                actorRole: role);

            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new RegisterStaffResponse(
                user.Id,
                user.AccountStatus.ToString(),
                "Registration received. You can sign in after the System Admin approves your account.");
        }

        private static Error ToRegistrationError(IdentityResult result)
        {
            var codes = result.Errors.Select(e => e.Code).ToList();
            var message = string.Join(" ", result.Errors.Select(e => e.Description));

            if (codes.Contains("DuplicateEmail") || codes.Contains("DuplicateUserName"))
            {
                return Error.Conflict("Registration.EmailTaken", "An account with this email already exists.");
            }

            return codes.Any(c => c.StartsWith("Password", StringComparison.Ordinal))
                ? Error.Validation("Registration.WeakPassword", message)
                : Error.Validation("Registration.Invalid", message);
        }
    }
}
