namespace BloodFlow.Domain.Enums
{
    /// <summary>How the expiry date was decided.</summary>
    public enum ExpiryBasis
    {
        /// <summary>Staff typed the expiry date printed on the bag.</summary>
        Entered,
        /// <summary>Calculated: collection date + the DefaultShelfLifeDays setting.</summary>
        DefaultShelfLife
    }
}
