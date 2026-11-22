namespace BloodFlow.Domain.Enums
{
    /// <summary>Where a batch came from. Slice 2 creates ManualIntake only; the others arrive with Slices 4 and 6.</summary>
    public enum BatchSourceType
    {
        ManualIntake,
        Donation,
        Transfer
    }
}
