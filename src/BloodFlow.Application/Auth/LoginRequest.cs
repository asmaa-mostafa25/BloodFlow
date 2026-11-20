using System.ComponentModel.DataAnnotations;

namespace BloodFlow.Application.Auth
{
    public sealed record LoginRequest
    {
        [Required, EmailAddress, StringLength(256)]
        public string Email { get; init; } = string.Empty;

        [Required, StringLength(128)]
        public string Password { get; init; } = string.Empty;
    }
}
