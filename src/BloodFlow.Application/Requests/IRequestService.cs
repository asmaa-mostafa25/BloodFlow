using BloodFlow.Application.Common;

namespace BloodFlow.Application.Requests
{
    /// <summary>
    /// The blood request lifecycle for hospital and blood bank staff
    /// (Phase 1 sections 3 and 4, workflows H1–H3 and B2–B3).
    /// Who may do what is decided from the caller's organization (token), never from the request body.
    /// </summary>
    public interface IRequestService
    {
        Task<Result<RequestDetailsDto>> CreateAsync(CreateBloodRequestRequest request, CancellationToken cancellationToken = default);

        /// <summary>Requests going TO my hospital.</summary>
        Task<Result<PagedResult<RequestSummaryDto>>> GetHospitalRequestsAsync(RequestQuery query, CancellationToken cancellationToken = default);

        /// <summary>Open requests my organization could fulfil (emergencies first).</summary>
        Task<Result<PagedResult<RequestSummaryDto>>> GetBoardAsync(BoardQuery query, CancellationToken cancellationToken = default);

        /// <summary>Requests my organization has reserved or fulfilled.</summary>
        Task<Result<PagedResult<RequestSummaryDto>>> GetExecutingAsync(RequestQuery query, CancellationToken cancellationToken = default);

        Task<Result<RequestDetailsDto>> GetAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<IReadOnlyList<StatusHistoryDto>>> GetHistoryAsync(int id, CancellationToken cancellationToken = default);

        Task<Result<RequestDetailsDto>> VerifyAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> ApproveAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> ReallocateAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> FulfilAsync(int id, FulfilRequestRequest request, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> ReceiveAsync(int id, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> RejectAsync(int id, string reason, CancellationToken cancellationToken = default);
        Task<Result<RequestDetailsDto>> CancelAsync(int id, string reason, CancellationToken cancellationToken = default);
    }

    /// <summary>System Admin monitoring (workflow A3) and emergency override review (section 4).</summary>
    public interface IRequestAdminService
    {
        Task<PagedResult<RequestSummaryDto>> GetAllAsync(RequestQuery query, CancellationToken cancellationToken = default);
        Task<PagedResult<EmergencyOverrideDto>> GetOverridesAsync(OverrideQuery query, CancellationToken cancellationToken = default);
        Task<Result> ReviewOverrideAsync(int overrideId, string? notes, CancellationToken cancellationToken = default);
    }

    /// <summary>Periodic clean-up: patient names are anonymized after the retention period.</summary>
    public interface IRequestMaintenanceService
    {
        Task<int> AnonymizePatientNamesAsync(CancellationToken cancellationToken = default);
    }
}
