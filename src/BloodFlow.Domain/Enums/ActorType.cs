namespace BloodFlow.Domain.Enums
{
    /// <summary>Who performed an audited action: a signed-in user or the system itself.</summary>
    public enum ActorType
    {
        User,
        System
    }
}
