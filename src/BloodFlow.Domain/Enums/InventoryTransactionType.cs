namespace BloodFlow.Domain.Enums
{
    /// <summary>
    /// The five stock operations from the Phase 1 general rules.
    /// Slice 2 uses Intake and Adjust; Reserve, Release and Deduct arrive with requests (Slice 3).
    /// </summary>
    public enum InventoryTransactionType
    {
        Intake,   // OnHand +n   (Receive / Add)
        Adjust,   // OnHand ±n   (reason required, never below Reserved)
        Reserve,  // Reserved +n
        Release,  // Reserved −n
        Deduct    // OnHand −n and Reserved −n
    }
}
