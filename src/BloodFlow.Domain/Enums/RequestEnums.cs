namespace BloodFlow.Domain.Enums
{
    public enum RequestType
    {
        Normal,
        Emergency
    }

    /// <summary>
    /// Pending → Approved → Fulfilled → Received, plus Rejected and Cancelled.
    /// Fulfilled = units deducted from stock; Received = the hospital confirmed delivery (no stock change).
    /// </summary>
    public enum RequestStatus
    {
        Pending,
        Approved,
        Fulfilled,
        Received,
        Rejected,
        Cancelled
    }

    /// <summary>Separate from the status: is this a real need, checked by a hospital?</summary>
    public enum VerificationStatus
    {
        Unverified,
        Verified
    }

    /// <summary>Who created the request: hospital staff, the patient, or a representative.</summary>
    public enum RequesterCapacity
    {
        HospitalStaff,
        Patient,
        Representative
    }

    public enum ReservationStatus
    {
        Active,
        Consumed,
        Released
    }

    public enum ReservationLineStatus
    {
        Active,
        Consumed,
        Released,
        Invalidated
    }

    public enum OverrideReviewStatus
    {
        PendingReview,
        Reviewed
    }
}
