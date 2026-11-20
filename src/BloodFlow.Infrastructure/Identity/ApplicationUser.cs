using BloodFlow.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace BloodFlow.Infrastructure.Identity
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = string.Empty;

        /// <summary>
        /// Only Active accounts can sign in. Staff start as Pending until approved.
        /// </summary>
        public AccountStatus AccountStatus { get; set; } = AccountStatus.Pending;

        /// <summary>UTC time the account was created.</summary>
        public DateTime CreatedAt { get; set; }
    }
}
