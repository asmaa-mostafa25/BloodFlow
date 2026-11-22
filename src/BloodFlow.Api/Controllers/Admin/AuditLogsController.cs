using BloodFlow.Application.Audit;
using BloodFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    /// <summary>Read-only audit trail. There are no endpoints to edit or delete audit rows.</summary>
    [ApiController]
    [Route("api/admin/audit-logs")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditLogService _auditLogs;

        public AuditLogsController(IAuditLogService auditLogs)
        {
            _auditLogs = auditLogs;
        }

        [HttpGet]
        public async Task<ActionResult<PagedResult<AuditLogDto>>> Get([FromQuery] AuditLogQuery query, CancellationToken cancellationToken) =>
            Ok(await _auditLogs.GetAsync(query, cancellationToken));
    }
}
