namespace BloodFlow.Domain.Enums
{
    /// <summary>Why staff changed a batch quantity by hand (workflow B1).</summary>
    public enum AdjustmentReason
    {
        /// <summary>Units broken or contaminated. Decrease only.</summary>
        Damaged,
        /// <summary>Expired units disposed of. Decrease only.</summary>
        Expired,
        /// <summary>A counting mistake fixed, up or down.</summary>
        Correction
    }
}
