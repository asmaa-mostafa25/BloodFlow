using BloodFlow.Application.Common;
using BloodFlow.Application.Inventory;
using BloodFlow.Application.Requests;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using BloodFlow.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Requests
{
    /// <summary>
    /// The blood request lifecycle (Phase 1 sections 3 and 4):
    ///
    ///   Pending ──approve──► Approved ──fulfil──► Fulfilled ──receive──► Received
    ///      │                    │
    ///      ├─reject─► Rejected  └─cancel─► Cancelled (the reservation is released)
    ///      └─cancel─► Cancelled
    ///
    /// Each state change writes, in ONE SaveChanges: the request, a status-history row,
    /// the stock moves (batch + ledger) and an audit row. All or nothing.
    /// </summary>
    internal sealed class RequestService : IRequestService
    {
        private const string BatchExpiredReason = "BatchExpired";
        private const string NextNumberSql = "SELECT NEXT VALUE FOR dbo." + BloodRequestConfiguration.NumberSequence + " AS [Value]";

        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly AuditLogger _audit;
        private readonly SettingsReader _settings;
        private readonly RequestReads _reads;

        public RequestService(
            AppDbContext db, ICurrentUser currentUser, IClock clock, AuditLogger audit, SettingsReader settings, RequestReads reads)
        {
            _db = db;
            _currentUser = currentUser;
            _clock = clock;
            _audit = audit;
            _settings = settings;
            _reads = reads;
        }

        // =====================================================================
        // Create (H1)
        // =====================================================================

        public async Task<Result<RequestDetailsDto>> CreateAsync(CreateBloodRequestRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;
            if (_currentUser.Role != Roles.HospitalStaff)
            {
                return Error.Forbidden("Request.OnlyHospitalStaff", "Only hospital staff can create a blood request.");
            }

            // Retry of the same submission: return what it already created.
            if (request.ClientRequestId.HasValue)
            {
                var previous = await _db.BloodRequests.AsNoTracking()
                    .Where(r => r.ClientRequestId == request.ClientRequestId.Value)
                    .Select(r => new { r.Id, r.CreatedByUserId })
                    .FirstOrDefaultAsync(cancellationToken);

                if (previous is not null)
                {
                    return previous.CreatedByUserId == userId
                        ? await DetailsAsync(previous.Id, cancellationToken)
                        : Error.Conflict("Request.DuplicateRequestId", "This clientRequestId was already used.");
                }
            }

            if (!await _db.BloodGroups.AnyAsync(g => g.Id == request.BloodGroupId, cancellationToken))
            {
                return Error.Validation("Request.BloodGroupNotFound", $"Blood group {request.BloodGroupId} does not exist.");
            }

            var maxUnits = await _settings.GetIntAsync(SettingKeys.MaxUnitsPerRequest, 10, cancellationToken);
            if (request.UnitsRequested > maxUnits)
            {
                return Error.Validation("Request.TooManyUnits", $"A single request may ask for at most {maxUnits} units.");
            }

            var now = _clock.UtcNow;
            var neededBy = ToUtc(request.NeededBy!.Value);
            if (neededBy <= now)
            {
                return Error.Validation("Request.NeededByInPast", "'Needed by' must be in the future.");
            }

            var patientName = request.PatientFullName.Trim();

            // Duplicate protection (section 3): the same patient, blood group and hospital
            // already has an OPEN request created within the window → refuse.
            var windowHours = await _settings.GetIntAsync(SettingKeys.DuplicateRequestWindowHours, 24, cancellationToken);
            var windowStart = now.AddHours(-windowHours);
            var duplicate = await _db.BloodRequests.AsNoTracking()
                .Where(r => r.DestinationHospitalId == orgId
                            && r.BloodGroupId == request.BloodGroupId
                            && r.PatientFullName == patientName // the column collation ignores case and Arabic diacritics
                            && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved)
                            && r.CreatedAt >= windowStart)
                .Select(r => r.RequestNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (duplicate is not null)
            {
                return Error.Conflict("Request.Duplicate",
                    $"An open request ({duplicate}) for this patient and blood group already exists. Update or cancel it instead.");
            }

            var entity = new BloodRequest
            {
                RequestNumber = await NextRequestNumberAsync(now, cancellationToken),
                RequestType = request.RequestType!.Value,
                Status = RequestStatus.Pending,
                BloodGroupId = request.BloodGroupId,
                UnitsRequested = request.UnitsRequested,
                NeededBy = neededBy,
                DestinationHospitalId = orgId,
                DestinationOrgType = OrganizationType.Hospital,
                CreatedByUserId = userId,
                RequesterCapacity = RequesterCapacity.HospitalStaff,
                PatientFullName = patientName,
                // Created by the hospital itself, for its own patient: verified at creation.
                // (Requests from patients/representatives arrive Unverified in a later slice.)
                VerificationStatus = VerificationStatus.Verified,
                VerifiedByUserId = userId,
                VerifiedAt = now,
                ClientRequestId = request.ClientRequestId,
                CreatedAt = now,
                StatusChangedAt = now
            };
            entity.StatusHistory.Add(new BloodRequestStatusHistory
            {
                FromStatus = null,
                ToStatus = RequestStatus.Pending,
                ChangedByUserId = userId,
                ChangedAt = now
            });

            // The audit row needs the new Id: save, audit, save — inside one transaction.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            _db.BloodRequests.Add(entity);
            await _db.SaveChangesAsync(cancellationToken);

            // Privacy: the patient name is NOT written to the audit log.
            _audit.Add(AuditActions.RequestCreated, AuditEntityTypes.BloodRequest, entity.Id.ToString(),
                newValues: new { entity.RequestNumber, entity.RequestType, entity.BloodGroupId, entity.UnitsRequested, entity.NeededBy },
                organizationId: orgId);
            await _db.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return await DetailsAsync(entity.Id, cancellationToken);
        }

        // =====================================================================
        // Lists
        // =====================================================================

        public async Task<Result<PagedResult<RequestSummaryDto>>> GetHospitalRequestsAsync(RequestQuery query, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out _, out var callerError)) return callerError!;

            var flags = await _reads.GetFlagContextAsync(cancellationToken);
            var requests = RequestReads.ApplyFilters(
                _db.BloodRequests.AsNoTracking().Where(r => r.DestinationHospitalId == orgId), query);
            if (query.OnlyFlagged) requests = flags.OnlyFlagged(requests);

            return await _reads.PageAsync(requests, query, flags, cancellationToken);
        }

        public async Task<Result<PagedResult<RequestSummaryDto>>> GetExecutingAsync(RequestQuery query, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out _, out var callerError)) return callerError!;

            var flags = await _reads.GetFlagContextAsync(cancellationToken);
            var requests = RequestReads.ApplyFilters(
                _db.BloodRequests.AsNoTracking().Where(r =>
                    r.Reservations.Any(x => x.OrganizationId == orgId && x.Status != ReservationStatus.Released)), query);
            if (query.OnlyFlagged) requests = flags.OnlyFlagged(requests);

            return await _reads.PageAsync(requests, query, flags, cancellationToken);
        }

        /// <summary>
        /// The board (workflow B2): open requests other organizations could fulfil.
        /// Emergencies first, then the earliest "needed by". Each row shows how many usable
        /// units of that blood group the caller's organization has right now.
        /// </summary>
        public async Task<Result<PagedResult<RequestSummaryDto>>> GetBoardAsync(BoardQuery query, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out _, out var callerError)) return callerError!;

            var myGovernorate = await _db.Organizations.AsNoTracking()
                .Where(o => o.Id == orgId).Select(o => o.GovernorateId).FirstAsync(cancellationToken);

            var requests = _db.BloodRequests.AsNoTracking().Where(r =>
                r.Status == RequestStatus.Pending
                && (r.VerificationStatus == VerificationStatus.Verified || r.RequestType == RequestType.Emergency)
                && r.DestinationHospitalId != orgId);

            if (!query.AllGovernorates) requests = requests.Where(r => r.DestinationHospital.GovernorateId == myGovernorate);
            if (query.Type.HasValue) requests = requests.Where(r => r.RequestType == query.Type.Value);
            if (query.BloodGroupId.HasValue) requests = requests.Where(r => r.BloodGroupId == query.BloodGroupId.Value);

            var total = await requests.CountAsync(cancellationToken);
            var rows = await requests
                .OrderByDescending(r => r.RequestType == RequestType.Emergency)
                .ThenBy(r => r.NeededBy)
                .ThenBy(r => r.Id)
                .Skip(query.Skip)
                .Take(query.SafePageSize)
                .Select(RequestRow.Projection)
                .ToListAsync(cancellationToken);

            var available = await AvailableByBloodGroupAsync(orgId, cancellationToken);
            var flags = await _reads.GetFlagContextAsync(cancellationToken);

            var items = rows.Select(r => flags.ToSummary(r, available.GetValueOrDefault(r.BloodGroupId))).ToList();
            return new PagedResult<RequestSummaryDto>(items, query.SafePage, query.SafePageSize, total);
        }

        // =====================================================================
        // Details
        // =====================================================================

        public async Task<Result<RequestDetailsDto>> GetAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;
            if (!await CanSeeAsync(id, orgId, userId, cancellationToken)) return NotFound(id);
            return await DetailsAsync(id, cancellationToken);
        }

        public async Task<Result<IReadOnlyList<StatusHistoryDto>>> GetHistoryAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;
            if (!await CanSeeAsync(id, orgId, userId, cancellationToken)) return NotFound(id);
            return Result<IReadOnlyList<StatusHistoryDto>>.Success(await _reads.GetHistoryAsync(id, cancellationToken));
        }

        // =====================================================================
        // Verify
        // =====================================================================

        /// <summary>The destination hospital confirms a request it did not create itself (e.g. from a patient).</summary>
        public async Task<Result<RequestDetailsDto>> VerifyAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null || request.DestinationHospitalId != orgId) return NotFound(id);

            if (request.VerificationStatus == VerificationStatus.Verified) return await DetailsAsync(id, cancellationToken);
            if (request.Status is not (RequestStatus.Pending or RequestStatus.Approved))
            {
                return InvalidState(request, "verify");
            }

            var now = _clock.UtcNow;
            request.VerificationStatus = VerificationStatus.Verified;
            request.VerifiedByUserId = userId;
            request.VerifiedAt = now;

            _audit.Add(AuditActions.RequestVerified, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { VerificationStatus = VerificationStatus.Unverified },
                newValues: new { request.VerificationStatus },
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        // =====================================================================
        // Approve = reserve stock (B2)
        // =====================================================================

        public async Task<Result<RequestDetailsDto>> ApproveAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null) return NotFound(id);

            if (request.DestinationHospitalId == orgId)
            {
                return Error.Conflict("Request.OwnRequest",
                    "Your organization created this request; another hospital or blood bank must approve it.");
            }
            if (!RequestRules.CanTransition(request.Status, RequestStatus.Approved)) return InvalidState(request, "approve");
            if (!RequestRules.CanReserve(request.RequestType, request.VerificationStatus))
            {
                return Error.Conflict("Request.NotVerified", "A normal request must be verified before it can be approved.");
            }

            var now = _clock.UtcNow;
            var correlationId = Guid.NewGuid();

            var reservation = new Reservation
            {
                RequestId = request.Id,
                BloodGroupId = request.BloodGroupId,
                OrganizationId = orgId,
                Status = ReservationStatus.Active,
                CreatedByUserId = userId,
                CreatedAt = now
            };

            var allocationError = await AllocateAsync(reservation, request.UnitsRequested, orgId, userId, correlationId, now, cancellationToken);
            if (allocationError is not null) return allocationError;

            _db.Reservations.Add(reservation);
            ChangeStatus(request, RequestStatus.Approved, userId, now, reason: null, correlationId);

            _audit.Add(AuditActions.RequestApproved, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { Status = RequestStatus.Pending },
                newValues: new
                {
                    request.Status,
                    ReservedUnits = request.UnitsRequested,
                    Batches = reservation.Lines.Select(l => new { l.BatchId, l.Quantity })
                },
                organizationId: orgId);

            // Two blood banks approving at the same moment: the request RowVersion and the
            // "one live reservation per request" unique index let only the first one win.
            return await SaveAndReturnAsync(id, cancellationToken);
        }

        /// <summary>
        /// After a reserved batch expired (its line was invalidated), top the reservation up
        /// again from other batches (FEFO). The still-valid lines are kept as they are.
        /// </summary>
        public async Task<Result<RequestDetailsDto>> ReallocateAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var (request, reservation, error) = await LoadForExecutorAsync(id, orgId, cancellationToken);
            if (error is not null) return error;
            if (request!.Status != RequestStatus.Approved) return InvalidState(request, "reallocate");

            var now = _clock.UtcNow;
            var correlationId = Guid.NewGuid();

            // Lines whose batch expired since the last check are invalidated first.
            InvalidateExpiredLines(reservation!, request, userId, correlationId, now);

            var activeUnits = reservation!.Lines.Where(l => l.Status == ReservationLineStatus.Active).Sum(l => l.Quantity);
            var missing = request.UnitsRequested - activeUnits;
            if (missing <= 0)
            {
                return Error.Conflict("Request.NothingToReallocate", "The reservation already covers the whole request.");
            }

            var allocationError = await AllocateAsync(reservation, missing, orgId, userId, correlationId, now, cancellationToken);
            if (allocationError is not null) return allocationError; // nothing saved

            _audit.Add(AuditActions.RequestReallocated, AuditEntityTypes.BloodRequest, id.ToString(),
                newValues: new { AddedUnits = missing, Batches = reservation.Lines.Where(l => l.Id == 0).Select(l => new { l.BatchId, l.Quantity }) },
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        // =====================================================================
        // Fulfil = blood leaves the bank (B3)
        // =====================================================================

        public async Task<Result<RequestDetailsDto>> FulfilAsync(int id, FulfilRequestRequest body, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var (request, reservation, error) = await LoadForExecutorAsync(id, orgId, cancellationToken);
            if (error is not null) return error;
            if (!RequestRules.CanTransition(request!.Status, RequestStatus.Fulfilled)) return InvalidState(request, "fulfil");

            var overrideReason = body.EmergencyOverrideReason?.Trim();
            var hasOverrideReason = !string.IsNullOrWhiteSpace(overrideReason);
            if (!RequestRules.CanFulfil(request.RequestType, request.VerificationStatus, hasOverrideReason))
            {
                return request.RequestType == RequestType.Emergency
                    ? Error.Validation("Request.OverrideReasonRequired",
                        "This emergency request is not verified yet. Give an emergency override reason to release the blood now.")
                    : Error.Conflict("Request.NotVerified", "The request must be verified before blood can be released.");
            }
            // An override is only used (and recorded) when it is actually needed.
            var usesOverride = request.VerificationStatus != VerificationStatus.Verified;

            var now = _clock.UtcNow;
            var correlationId = Guid.NewGuid();

            // Never send expired blood: if a reserved batch expired, invalidate its line, save
            // that, and stop. Staff then reallocate and try again.
            if (InvalidateExpiredLines(reservation!, request, userId, correlationId, now) > 0)
            {
                await _db.SaveChangesAsync(cancellationToken);
                return Error.Conflict("Request.ReservationInvalidated",
                    "Some reserved units expired and were released. Reallocate the reservation, then fulfil again.");
            }

            var activeLines = reservation!.Lines.Where(l => l.Status == ReservationLineStatus.Active).ToList();
            if (activeLines.Sum(l => l.Quantity) != request.UnitsRequested)
            {
                return Error.Conflict("Request.NeedsReallocation",
                    "The reservation no longer covers the whole request. Reallocate it first.");
            }

            foreach (var line in activeLines)
            {
                _db.InventoryTransactions.Add(ReservationLedger.Deduct(line.Batch, line, userId, correlationId, now));
            }

            reservation.Status = ReservationStatus.Consumed;
            reservation.ClosedAt = now;

            if (usesOverride)
            {
                request.EmergencyOverrideUsed = true;
                _db.EmergencyOverrides.Add(new EmergencyOverride
                {
                    RequestId = request.Id,
                    RequestType = RequestType.Emergency,
                    OrganizationId = orgId,
                    OverriddenByUserId = userId,
                    ReasonText = overrideReason!,
                    OverriddenAt = now,
                    ReviewStatus = OverrideReviewStatus.PendingReview
                });
                _audit.Add(AuditActions.EmergencyOverrideUsed, AuditEntityTypes.BloodRequest, id.ToString(),
                    newValues: new { request.VerificationStatus, ReviewStatus = OverrideReviewStatus.PendingReview },
                    reason: overrideReason,
                    organizationId: orgId);
            }

            ChangeStatus(request, RequestStatus.Fulfilled, userId, now, usesOverride ? overrideReason : null, correlationId);

            _audit.Add(AuditActions.RequestFulfilled, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { Status = RequestStatus.Approved },
                newValues: new { request.Status, Units = request.UnitsRequested, EmergencyOverride = usesOverride },
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        // =====================================================================
        // Receive (H2)
        // =====================================================================

        public async Task<Result<RequestDetailsDto>> ReceiveAsync(int id, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null || (request.DestinationHospitalId != orgId && request.CreatedByUserId != userId)) return NotFound(id);

            // Pressing "received" twice is harmless.
            if (request.Status == RequestStatus.Received) return await DetailsAsync(id, cancellationToken);
            if (!RequestRules.CanTransition(request.Status, RequestStatus.Received)) return InvalidState(request, "confirm receipt of");

            var now = _clock.UtcNow;
            ChangeStatus(request, RequestStatus.Received, userId, now, reason: null, correlationId: null);

            _audit.Add(AuditActions.RequestReceived, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { Status = RequestStatus.Fulfilled },
                newValues: new { request.Status },
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        // =====================================================================
        // Reject / Cancel
        // =====================================================================

        /// <summary>
        /// Reject a pending request: by the destination hospital (e.g. entered by mistake, not a real need)
        /// or by a blood bank (e.g. the request is invalid). The reason is mandatory.
        /// </summary>
        public async Task<Result<RequestDetailsDto>> RejectAsync(int id, string reason, CancellationToken cancellationToken = default)
        {
            if (!TryGetCaller(out var orgId, out var userId, out var callerError)) return callerError!;

            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null) return NotFound(id);

            var allowed = request.DestinationHospitalId == orgId || _currentUser.Role == Roles.BloodBankStaff;
            if (!allowed)
            {
                return Error.Forbidden("Request.CannotReject", "Only the destination hospital or a blood bank can reject a request.");
            }
            if (!RequestRules.CanTransition(request.Status, RequestStatus.Rejected)) return InvalidState(request, "reject");

            var now = _clock.UtcNow;
            ChangeStatus(request, RequestStatus.Rejected, userId, now, reason.Trim(), correlationId: null);

            _audit.Add(AuditActions.RequestRejected, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { Status = RequestStatus.Pending },
                newValues: new { request.Status },
                reason: reason.Trim(),
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        /// <summary>
        /// Cancel: while Pending (creator, destination hospital, admin) or Approved
        /// (creator, destination hospital, executing organization — not the admin).
        /// Cancelling an approved request releases its stock.
        /// </summary>
        public async Task<Result<RequestDetailsDto>> CancelAsync(int id, string reason, CancellationToken cancellationToken = default)
        {
            var userId = _currentUser.UserId;
            if (userId is null) return Error.Unauthorized("Auth.Required", "Sign in first.");
            var orgId = _currentUser.OrganizationId;
            var isAdmin = _currentUser.Role == Roles.SystemAdmin;

            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null) return NotFound(id);

            Reservation? reservation = null;
            if (request.Status == RequestStatus.Approved)
            {
                reservation = await _db.Reservations
                    .Include(x => x.Lines).ThenInclude(l => l.Batch)
                    .FirstOrDefaultAsync(x => x.RequestId == id && x.Status == ReservationStatus.Active, cancellationToken);
            }

            var isParty = isAdmin
                          || request.CreatedByUserId == userId
                          || request.DestinationHospitalId == orgId
                          || (reservation is not null && reservation.OrganizationId == orgId);
            if (!isParty)
            {
                return Error.Forbidden("Request.CannotCancel", "Only the requester, the destination hospital, the executing organization or an admin can cancel.");
            }
            if (!RequestRules.CanTransition(request.Status, RequestStatus.Cancelled)) return InvalidState(request, "cancel");

            // Section 3: the admin may cancel only PENDING requests. Cancelling an approved one
            // releases stock, and the admin never changes stock (A3).
            var isOtherParty = request.CreatedByUserId == userId || request.DestinationHospitalId == orgId
                               || (reservation is not null && reservation.OrganizationId == orgId);
            if (isAdmin && !isOtherParty && request.Status != RequestStatus.Pending)
            {
                return Error.Conflict("Request.AdminCannotCancelApproved",
                    "An approved request holds stock; only the requester, the hospital or the executing organization can cancel it.");
            }

            var now = _clock.UtcNow;
            var correlationId = Guid.NewGuid();
            var releasedUnits = 0;

            if (reservation is not null)
            {
                foreach (var line in reservation.Lines.Where(l => l.Status == ReservationLineStatus.Active))
                {
                    releasedUnits += line.Quantity;
                    _db.InventoryTransactions.Add(ReservationLedger.Release(
                        line.Batch, line, ReservationLineStatus.Released, invalidationReason: null, userId, correlationId, now));
                }
                reservation.Status = ReservationStatus.Released;
                reservation.ClosedAt = now;
            }

            var oldStatus = request.Status;
            ChangeStatus(request, RequestStatus.Cancelled, userId.Value, now, reason.Trim(), correlationId);

            _audit.Add(AuditActions.RequestCancelled, AuditEntityTypes.BloodRequest, id.ToString(),
                oldValues: new { Status = oldStatus },
                newValues: new { request.Status, ReleasedUnits = releasedUnits },
                reason: reason.Trim(),
                organizationId: orgId);

            return await SaveAndReturnAsync(id, cancellationToken);
        }

        // =====================================================================
        // helpers
        // =====================================================================

        /// <summary>
        /// Reserve <paramref name="units"/> from the organization's usable batches, FEFO.
        /// Availability is calculated in memory from TRACKED batches, so units released earlier
        /// in the same operation are already counted. Returns an error (and changes nothing
        /// that will be saved) when there is not enough stock: no partial approvals.
        /// </summary>
        private async Task<Error?> AllocateAsync(
            Reservation reservation, int units, int orgId, int userId, Guid correlationId, DateTime now, CancellationToken cancellationToken)
        {
            var today = _clock.Today;
            var batches = await _db.BloodBatches
                .Where(b => b.OrganizationId == orgId
                            && b.BloodGroupId == reservation.BloodGroupId
                            && b.Status == BatchStatus.Active
                            && !(b.ExpiryDate < today)) // same rule as InventoryRules.IsExpired
                .ToListAsync(cancellationToken);

            var candidates = batches
                .Select(b => new BatchCandidate(b.Id, b.ExpiryDate, b.OnHandQuantity - b.ReservedQuantity))
                .ToList();

            var allocation = RequestRules.AllocateFefo(candidates, units);
            if (allocation is null)
            {
                var available = candidates.Sum(c => Math.Max(0, c.Available));
                return Error.Conflict("Request.InsufficientStock",
                    $"Not enough usable units: {units} needed, {available} available. Nothing was reserved.");
            }

            var byId = batches.ToDictionary(b => b.Id);
            foreach (var part in allocation)
            {
                var batch = byId[part.BatchId];
                var line = new ReservationLine
                {
                    BatchId = batch.Id,
                    Batch = batch,
                    OrganizationId = orgId,
                    BloodGroupId = reservation.BloodGroupId,
                    Quantity = part.Quantity,
                    Status = ReservationLineStatus.Active
                };
                reservation.Lines.Add(line);
                _db.InventoryTransactions.Add(ReservationLedger.Reserve(batch, line, userId, correlationId, now));
            }

            return null;
        }

        /// <summary>
        /// Lines on a batch that is expired (by status or by date) are released with status
        /// Invalidated. Returns how many lines were invalidated.
        /// </summary>
        private int InvalidateExpiredLines(Reservation reservation, BloodRequest request, int userId, Guid correlationId, DateTime now)
        {
            var today = _clock.Today;
            var count = 0;

            foreach (var line in reservation.Lines.Where(l => l.Status == ReservationLineStatus.Active).ToList())
            {
                var batch = line.Batch;
                var expired = batch.Status == BatchStatus.Expired || InventoryRules.IsExpired(batch.ExpiryDate, today);
                if (!expired) continue;

                if (batch.Status == BatchStatus.Active)
                {
                    // The hourly job has not marked it yet; do it now.
                    batch.Status = BatchStatus.Expired;
                    batch.ExpiredAt = now;
                }

                _db.InventoryTransactions.Add(ReservationLedger.Release(
                    batch, line, ReservationLineStatus.Invalidated, BatchExpiredReason, userId, correlationId, now));

                _audit.Add(AuditActions.ReservationLineInvalidated, AuditEntityTypes.BloodRequest, request.Id.ToString(),
                    newValues: new { line.BatchId, line.Quantity, Reason = BatchExpiredReason },
                    organizationId: reservation.OrganizationId);
                count++;
            }

            return count;
        }

        /// <summary>Loads the request and its live reservation, for the organization that holds it.</summary>
        private async Task<(BloodRequest? Request, Reservation? Reservation, Error? Error)> LoadForExecutorAsync(
            int id, int orgId, CancellationToken cancellationToken)
        {
            var request = await _db.BloodRequests.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
            if (request is null) return (null, null, NotFound(id));

            var reservation = await _db.Reservations
                .Include(x => x.Lines).ThenInclude(l => l.Batch)
                .FirstOrDefaultAsync(x => x.RequestId == id && x.Status != ReservationStatus.Released, cancellationToken);

            if (reservation is null || reservation.OrganizationId != orgId)
            {
                return (null, null, Error.Forbidden("Request.NotExecutor",
                    "Only the organization that approved (reserved) this request can do this."));
            }

            return (request, reservation, null);
        }

        private void ChangeStatus(BloodRequest request, RequestStatus to, int userId, DateTime now, string? reason, Guid? correlationId)
        {
            _db.BloodRequestStatusHistory.Add(new BloodRequestStatusHistory
            {
                RequestId = request.Id,
                FromStatus = request.Status,
                ToStatus = to,
                ChangedByUserId = userId,
                Reason = reason,
                ChangedAt = now,
                CorrelationId = correlationId
            });
            request.Status = to;
            request.StatusChangedAt = now;
        }

        private async Task<Result<RequestDetailsDto>> SaveAndReturnAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("Request.Concurrency",
                    "This request or its stock was changed at the same time by someone else. Reload and try again.");
            }

            return await DetailsAsync(id, cancellationToken);
        }

        /// <summary>Details for the caller; the patient name only for the destination hospital and the requester.</summary>
        private async Task<Result<RequestDetailsDto>> DetailsAsync(int id, CancellationToken cancellationToken)
        {
            var orgId = _currentUser.OrganizationId;
            var userId = _currentUser.UserId;

            var details = await _reads.GetDetailsAsync(id,
                (row, createdBy) => row.DestinationHospitalId == orgId || createdBy == userId,
                cancellationToken);

            return details is null ? NotFound(id) : details;
        }

        /// <summary>
        /// Who may open a request: the destination hospital, the requester, the executing
        /// organization, and — while it is on the board (Pending) — any staff member.
        /// Everyone else gets 404, so request numbers do not leak.
        /// </summary>
        private Task<bool> CanSeeAsync(int id, int orgId, int userId, CancellationToken cancellationToken) =>
            _db.BloodRequests.AsNoTracking().AnyAsync(r => r.Id == id && (
                r.DestinationHospitalId == orgId
                || r.CreatedByUserId == userId
                || r.Status == RequestStatus.Pending
                || r.Reservations.Any(x => x.OrganizationId == orgId)), cancellationToken);

        private async Task<Dictionary<byte, int>> AvailableByBloodGroupAsync(int orgId, CancellationToken cancellationToken)
        {
            var today = _clock.Today;
            return await _db.BloodBatches.AsNoTracking()
                .Where(b => b.OrganizationId == orgId && b.Status == BatchStatus.Active && !(b.ExpiryDate < today))
                .GroupBy(b => b.BloodGroupId)
                .Select(g => new { g.Key, Available = g.Sum(b => b.OnHandQuantity - b.ReservedQuantity) })
                .ToDictionaryAsync(x => x.Key, x => x.Available, cancellationToken);
        }

        /// <summary>BR-2026-000123. The number comes from a SQL sequence, so it is unique under load.</summary>
        private async Task<string> NextRequestNumberAsync(DateTime now, CancellationToken cancellationToken)
        {
            // No LINQ operator after SqlQuery: the SQL is sent as written (NEXT VALUE FOR is not allowed in a subquery).
            var values = await _db.Database
                .SqlQueryRaw<int>(NextNumberSql)
                .ToListAsync(cancellationToken);
            return $"BR-{now:yyyy}-{values[0]:D6}";
        }

        private static DateTime ToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc) // no offset sent: treat as UTC (documented in the API)
        };

        private bool TryGetCaller(out int organizationId, out int userId, out Error? error)
        {
            organizationId = _currentUser.OrganizationId ?? 0;
            userId = _currentUser.UserId ?? 0;
            error = null;

            if (_currentUser.OrganizationId is null || _currentUser.UserId is null)
            {
                error = Error.Forbidden("Request.NoOrganization", "Only hospital or blood bank staff can do this.");
                return false;
            }
            return true;
        }

        private static Error NotFound(int id) => Error.NotFound("Request.NotFound", $"Request {id} was not found.");

        private static Error InvalidState(BloodRequest request, string action) =>
            Error.Conflict("Request.InvalidState", $"Cannot {action} a request that is {request.Status}.");
    }
}
