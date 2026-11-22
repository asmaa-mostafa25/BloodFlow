using BloodFlow.Api.Common;
using BloodFlow.Application.Common;
using BloodFlow.Application.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/settings")]
    [Authorize(Roles = Roles.SystemAdmin)]
    [Produces("application/json")]
    public class SettingsController : ControllerBase
    {
        private readonly ISystemSettingService _settings;

        public SettingsController(ISystemSettingService settings)
        {
            _settings = settings;
        }

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<SystemSettingDto>>> GetAll(CancellationToken cancellationToken) =>
            Ok(await _settings.GetAllAsync(cancellationToken));

        [HttpGet("{key}")]
        [ProducesResponseType<SystemSettingDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Get(string key, CancellationToken cancellationToken) =>
            (await _settings.GetAsync(key, cancellationToken)).ToActionResult(this);

        /// <summary>Medical parameters require an approvalReference.</summary>
        [HttpPut("{key}")]
        [ProducesResponseType<SystemSettingDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Update(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken) =>
            (await _settings.UpdateAsync(key, request, cancellationToken)).ToActionResult(this);
    }
}
