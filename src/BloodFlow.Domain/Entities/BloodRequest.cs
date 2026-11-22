using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// A NEED for blood ("3 units of O+ for this patient before tomorrow 6 pm").
    /// It does not hold stock itself: the units are held by a <see cref="Reservation"/>.
    /// </summary>
    public class BloodRequest
    {
        public int Id { get; set; }

        /// <summary>Human-friendly number, e.g. BR-2026-000123.</summary>
        public required string RequestNumber { get; set; }

        public RequestType RequestType { get; set; }
        public RequestStatus Status { get; set; } = RequestStatus.Pending;
        public VerificationStatus VerificationStatus { get; set; }

        public byte BloodGroupId { get; set; }
        public BloodGroup BloodGroup { get; set; } = null!;

        public int UnitsRequested { get; set; }

        /// <summary>UTC time the blood is needed by.</summary>
        public DateTime NeededBy { get; set; }

        /// <summary>The hospital that will receive the blood.</summary>
        public int DestinationHospitalId { get; set; }
        public Organization DestinationHospital { get; set; } = null!;

        /// <summary>Always Hospital. Exists so a composite FK can guarantee the destination is a hospital.</summary>
        public OrganizationType DestinationOrgType { get; set; } = OrganizationType.Hospital;

        public int CreatedByUserId { get; set; }
        public RequesterCapacity RequesterCapacity { get; set; }

        /// <summary>Patient privacy: cleared after the retention period (PatientAnonymizedAt is then set).</summary>
        public string? PatientFullName { get; set; }
        public DateTime? PatientAnonymizedAt { get; set; }
        public string? RelationshipToPatient { get; set; }

        public int? VerifiedByUserId { get; set; }
        public DateTime? VerifiedAt { get; set; }

        public bool EmergencyOverrideUsed { get; set; }

        /// <summary>Makes "create request" safe to retry (double click, network retry).</summary>
        public Guid? ClientRequestId { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>When the status last changed. Drives the overdue / unconfirmed / escalation flags.</summary>
        public DateTime StatusChangedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
        public ICollection<BloodRequestStatusHistory> StatusHistory { get; set; } = new List<BloodRequestStatusHistory>();
    }
}
