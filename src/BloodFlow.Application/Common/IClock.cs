namespace BloodFlow.Application.Common
{
    /// <summary>
    /// The current time. Behind an interface so tests can fix "today",
    /// and so "today" means today in Egypt, not on the server's clock.
    /// </summary>
    public interface IClock
    {
        DateTime UtcNow { get; }

        /// <summary>Today's date in the business time zone (Africa/Cairo).</summary>
        DateOnly Today { get; }
    }
}
