namespace BloodFlow.Application.Auth
{
    /// <summary>What the frontend gets after a successful login.</summary>
    public sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, AuthUserDto User);

    /// <summary>
    /// Basic info about the signed-in user, so the frontend can show the right screens
    /// without decoding the token.
    /// </summary>
    public sealed record AuthUserDto(int Id, string FullName, string Email, string Role, int? OrganizationId);
}
