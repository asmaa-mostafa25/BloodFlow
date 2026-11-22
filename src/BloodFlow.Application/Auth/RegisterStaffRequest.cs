using System.ComponentModel.DataAnnotations;

namespace BloodFlow.Application.Auth
{
    /// <summary>
    /// Self-registration of a Hospital or Blood Bank staff member (workflow A1).
    /// The staff kind is NOT sent by the client: it is taken from the chosen organization.
    /// </summary>
    public sealed record RegisterStaffRequest
    {
        [Required, StringLength(200, MinimumLength = 3)]
        public string FullName { get; init; } = string.Empty;

        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; } = string.Empty;

        /// <summary>Password rules (length, digit, upper/lower case) are checked by Identity.</summary>
        [Required, StringLength(128, MinimumLength = 8)]
        public string Password { get; init; } = string.Empty;

        [Range(1, int.MaxValue)]
        public int OrganizationId { get; init; }

        [Required, StringLength(100, MinimumLength = 2)]
        public string JobTitle { get; init; } = string.Empty;
    }

    public sealed record RegisterStaffResponse(int UserId, string AccountStatus, string Message);
}
