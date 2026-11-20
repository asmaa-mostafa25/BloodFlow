namespace BloodFlow.Application.Common
{
    /// <summary>
    /// The claim names inside our JWT. Shared by the token generator (Infrastructure)
    /// and the code that reads the current user (Api), so both always agree.
    /// </summary>
    public static class AppClaimTypes
    {
        public const string UserId = "sub";
        public const string Email = "email";
        public const string Name = "name";
        public const string Role = "role";
        public const string OrganizationId = "org_id";
    }
}
