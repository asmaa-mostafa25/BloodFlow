namespace BloodFlow.Domain.Enums
{
    /// <summary>
    /// Active = usable stock. Expired = past its expiry date; excluded from Available.
    /// Expired units stay counted in OnHand until staff dispose of them with an adjustment.
    /// </summary>
    public enum BatchStatus
    {
        Active,
        Expired
    }
}
