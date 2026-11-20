namespace BloodFlow.Domain.Enums
{
    /// <summary>
    /// Lifecycle of a user account (Phase 1, workflow A1).
    /// Staff accounts start as Pending until a System Admin approves them.
    /// </summary>
    public enum AccountStatus
    {
        Pending,
        Active,
        Rejected,
        Deactivated
    }
}
