using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// Section 4: an emergency request may be fulfilled before it is verified, only with a
    /// mandatory reason. Every override is recorded here and flagged for admin review.
    /// </summary>
    public class EmergencyOverride
    {
        public int Id { get; set; }

        public int RequestId { get; set; }
        public BloodRequest Request { get; set; } = null!;

        /// <summary>Always Emergency (a composite FK guarantees only emergency requests can be overridden).</summary>
        public RequestType RequestType { get; set; } = RequestType.Emergency;

        public int OrganizationId { get; set; }
        public int OverriddenByUserId { get; set; }
        public required string ReasonText { get; set; }
        public DateTime OverriddenAt { get; set; }

        public OverrideReviewStatus ReviewStatus { get; set; } = OverrideReviewStatus.PendingReview;
        public int? ReviewedByUserId { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewNotes { get; set; }
    }
}
