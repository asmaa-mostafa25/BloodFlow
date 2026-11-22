using System.Linq.Expressions;
using BloodFlow.Application.Common;
using BloodFlow.Application.Requests;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Requests
{
    /// <summary>What SQL returns for one request in a list. The flags are added in C# afterwards.</summary>
    internal sealed record RequestRow(
        int Id, string RequestNumber, RequestType RequestType, RequestStatus Status, VerificationStatus VerificationStatus,
        byte BloodGroupId, string BloodGroupCode, int UnitsRequested, DateTime NeededBy,
        int DestinationHospitalId, string DestinationHospitalName, string GovernorateName,
        int? ExecutingOrganizationId, string? ExecutingOrganizationName,
        DateTime CreatedAt, DateTime StatusChangedAt)
    {
        /// <summary>
        /// The "executing" organization is the one holding the live (Active or Consumed) reservation.
        /// A released reservation (cancelled) does not count.
        /// </summary>
        public static readonly Expression<Func<BloodRequest, RequestRow>> Projection = r => new RequestRow(
            r.Id, r.RequestNumber, r.RequestType, r.Status, r.VerificationStatus,
            r.BloodGroupId, r.BloodGroup.Code, r.UnitsRequested, r.NeededBy,
            r.DestinationHospitalId, r.DestinationHospital.Name, r.DestinationHospital.Governorate.Name,
            r.Reservations.Where(x => x.Status != ReservationStatus.Released).Select(x => (int?)x.OrganizationId).FirstOrDefault(),
            r.Reservations.Where(x => x.Status != ReservationStatus.Released).Select(x => x.Organization.Name).FirstOrDefault(),
            r.CreatedAt, r.StatusChangedAt);
    }

    /// <summary>The flag thresholds (from SystemSettings) and "now", read once per call.</summary>
    internal sealed record FlagContext(DateTime Now, int EscalationMinutes, int ReceiptHours)
    {
        public RequestSummaryDto ToSummary(RequestRow r, int? availableAtYourOrganization = null) => new(
            r.Id, r.RequestNumber, r.RequestType, r.Status, r.VerificationStatus,
            r.BloodGroupId, r.BloodGroupCode, r.UnitsRequested, r.NeededBy,
            r.DestinationHospitalId, r.DestinationHospitalName, r.GovernorateName,
            r.ExecutingOrganizationId, r.ExecutingOrganizationName,
            r.CreatedAt, r.StatusChangedAt,
            RequestRules.ComputeFlags(r.Status, r.RequestType, r.NeededBy, r.CreatedAt, r.StatusChangedAt,
                Now, EscalationMinutes, ReceiptHours),
            availableAtYourOrganization);

        /// <summary>
        /// The same three rules as <see cref="RequestRules.ComputeFlags"/>, written as a SQL filter
        /// so "only flagged" can be paged in the database.
        /// </summary>
        public IQueryable<BloodRequest> OnlyFlagged(IQueryable<BloodRequest> requests)
        {
            var now = Now;
            var receiptLimit = Now.AddHours(-ReceiptHours);
            var escalationLimit = Now.AddMinutes(-EscalationMinutes);

            return requests.Where(r =>
                (r.Status == RequestStatus.Approved && r.NeededBy < now)
                || (r.Status == RequestStatus.Fulfilled && r.StatusChangedAt < receiptLimit)
                || (r.RequestType == RequestType.Emergency && r.Status == RequestStatus.Pending && r.CreatedAt < escalationLimit));
        }
    }

    /// <summary>Shared read helpers for the staff service and the admin service.</summary>
    internal sealed class RequestReads
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly SettingsReader _settings;

        public RequestReads(AppDbContext db, IClock clock, SettingsReader settings)
        {
            _db = db;
            _clock = clock;
            _settings = settings;
        }

        public async Task<FlagContext> GetFlagContextAsync(CancellationToken cancellationToken) => new(
            _clock.UtcNow,
            await _settings.GetIntAsync(SettingKeys.EmergencyEscalationMinutes, 30, cancellationToken),
            await _settings.GetIntAsync(SettingKeys.ReceiptConfirmationHours, 48, cancellationToken));

        public static IQueryable<BloodRequest> ApplyFilters(IQueryable<BloodRequest> requests, RequestQuery query)
        {
            if (query.Status.HasValue) requests = requests.Where(r => r.Status == query.Status.Value);
            if (query.Type.HasValue) requests = requests.Where(r => r.RequestType == query.Type.Value);
            if (query.BloodGroupId.HasValue) requests = requests.Where(r => r.BloodGroupId == query.BloodGroupId.Value);
            return requests;
        }

        /// <summary>Count, order (newest first) and page, then turn rows into DTOs with flags.</summary>
        public async Task<PagedResult<RequestSummaryDto>> PageAsync(
            IQueryable<BloodRequest> requests, PageQuery query, FlagContext flags, CancellationToken cancellationToken)
        {
            var total = await requests.CountAsync(cancellationToken);
            var rows = await requests
                .OrderByDescending(r => r.Id)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(RequestRow.Projection)
                .ToListAsync(cancellationToken);

            return new PagedResult<RequestSummaryDto>(
                rows.Select(r => flags.ToSummary(r)).ToList(), query.SafePage, query.SafePageSize, total);
        }

        /// <summary>
        /// Full details. <paramref name="showPatientName"/> is decided by the caller:
        /// only the destination hospital and the person who created the request may see the name.
        /// </summary>
        public async Task<RequestDetailsDto?> GetDetailsAsync(int id, Func<RequestRow, int, bool> showPatientName, CancellationToken cancellationToken)
        {
            var data = await _db.BloodRequests.AsNoTracking()
                .Where(r => r.Id == id)
                .Select(r => new
                {
                    Row = new RequestRow(
                        r.Id, r.RequestNumber, r.RequestType, r.Status, r.VerificationStatus,
                        r.BloodGroupId, r.BloodGroup.Code, r.UnitsRequested, r.NeededBy,
                        r.DestinationHospitalId, r.DestinationHospital.Name, r.DestinationHospital.Governorate.Name,
                        r.Reservations.Where(x => x.Status != ReservationStatus.Released).Select(x => (int?)x.OrganizationId).FirstOrDefault(),
                        r.Reservations.Where(x => x.Status != ReservationStatus.Released).Select(x => x.Organization.Name).FirstOrDefault(),
                        r.CreatedAt, r.StatusChangedAt),
                    r.RequesterCapacity,
                    r.CreatedByUserId,
                    r.PatientFullName,
                    r.PatientAnonymizedAt,
                    r.VerifiedByUserId,
                    r.VerifiedAt,
                    r.EmergencyOverrideUsed
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (data is null) return null;

            // The live reservation, or (after a cancel) the last released one, for the record.
            var reservation = await _db.Reservations.AsNoTracking()
                .Where(x => x.RequestId == id)
                .OrderBy(x => x.Status == ReservationStatus.Released ? 1 : 0)
                .ThenByDescending(x => x.Id)
                .Select(x => new
                {
                    x.Id,
                    x.OrganizationId,
                    OrganizationName = x.Organization.Name,
                    x.Status,
                    x.CreatedAt,
                    x.ClosedAt,
                    Lines = x.Lines
                        .OrderBy(l => l.Batch.ExpiryDate).ThenBy(l => l.Id)
                        .Select(l => new ReservationLineDto(l.Id, l.BatchId, l.Batch.ExpiryDate, l.Quantity, l.Status, l.InvalidationReason))
                        .ToList()
                })
                .FirstOrDefaultAsync(cancellationToken);

            ReservationDto? reservationDto = null;
            if (reservation is not null)
            {
                var activeUnits = reservation.Lines.Where(l => l.Status == ReservationLineStatus.Active).Sum(l => l.Quantity);
                reservationDto = new ReservationDto(
                    reservation.Id, reservation.OrganizationId, reservation.OrganizationName, reservation.Status,
                    activeUnits,
                    // A line was invalidated (expired batch) and the hold no longer covers the request.
                    NeedsReallocation: reservation.Status == ReservationStatus.Active && activeUnits < data.Row.UnitsRequested,
                    reservation.CreatedAt, reservation.ClosedAt, reservation.Lines);
            }

            var flags = await GetFlagContextAsync(cancellationToken);

            return new RequestDetailsDto(
                flags.ToSummary(data.Row),
                data.RequesterCapacity,
                data.CreatedByUserId,
                showPatientName(data.Row, data.CreatedByUserId) ? data.PatientFullName : null,
                data.PatientAnonymizedAt.HasValue,
                data.VerifiedByUserId,
                data.VerifiedAt,
                data.EmergencyOverrideUsed,
                reservationDto);
        }

        public async Task<IReadOnlyList<StatusHistoryDto>> GetHistoryAsync(int id, CancellationToken cancellationToken) =>
            await _db.BloodRequestStatusHistory.AsNoTracking()
                .Where(h => h.RequestId == id)
                .OrderBy(h => h.Id)
                .Select(h => new StatusHistoryDto(h.FromStatus, h.ToStatus, h.ChangedByUserId, h.Reason, h.ChangedAt))
                .ToListAsync(cancellationToken);
    }
}
