namespace BloodFlow.Application.Common
{
    /// <summary>
    /// Who is calling right now, read from the JWT. Implemented in the Api project,
    /// because only the web layer knows about the HTTP request.
    /// </summary>
    public interface ICurrentUser
    {
        int? UserId { get; }
        string? Role { get; }
        string? IpAddress { get; }
    }
}
