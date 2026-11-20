using BloodFlow.Application.Common;
using BloodFlow.Application.Settings;
using BloodFlow.Domain.Entities;
using BloodFlow.Infrastructure.Common;
using BloodFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Infrastructure.Services
{
    internal class SystemSettingService : ISystemSettingService
    {
        private readonly AppDbContext _db;
        private readonly AuditLogger _audit;
        private readonly ICurrentUser _currentUser;

        public SystemSettingService(AppDbContext db, AuditLogger audit, ICurrentUser currentUser)
        {
            _db = db;
            _audit = audit;
            _currentUser = currentUser;
        }

        public async Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var settings = await _db.SystemSettings.AsNoTracking()
                .OrderBy(s => s.SettingKey)
                .ToListAsync(cancellationToken);
            return settings.Select(ToDto).ToList();
        }

        public async Task<Result<SystemSettingDto>> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            var setting = await _db.SystemSettings.AsNoTracking()
                .FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);

            if (setting is null)
            {
                return NotFound(key);
            }
            return ToDto(setting);
        }

        public async Task<Result<SystemSettingDto>> UpdateAsync(string key, UpdateSystemSettingRequest request, CancellationToken cancellationToken = default)
        {
            if (!RowVersionConverter.TryParse(request.RowVersion, out var rowVersion))
            {
                return Error.Validation("Settings.InvalidRowVersion", "RowVersion is missing or invalid.");
            }

            var setting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.SettingKey == key, cancellationToken);
            if (setting is null) return NotFound(key);

            if (!SettingValueValidator.TryNormalize(setting.ValueType, request.Value, out var value, out var validationError))
            {
                return Error.Validation("Settings.InvalidValue", validationError!);
            }

            // Medical parameters (age, interval, shelf life...) need a clinical approval reference.
            var approvalReference = request.ApprovalReference?.Trim();
            if (setting.IsMedicalParameter && string.IsNullOrEmpty(approvalReference))
            {
                return Error.Validation("Settings.ApprovalRequired",
                    "This is a medical parameter. Provide the approval reference (who approved the new value).");
            }

            _db.Entry(setting).Property(s => s.RowVersion).OriginalValue = rowVersion;

            var before = new { setting.SettingValue, setting.ApprovalReference };
            var now = DateTime.UtcNow;

            setting.SettingValue = value;
            setting.UpdatedByUserId = _currentUser.UserId;
            setting.UpdatedAt = now;
            if (setting.IsMedicalParameter)
            {
                setting.ApprovalReference = approvalReference;
                setting.ApprovedByUserId = _currentUser.UserId;
                setting.ApprovedAt = now;
            }

            _audit.Add(AuditActions.SettingUpdated, AuditEntityTypes.SystemSetting, key,
                oldValues: before,
                newValues: new { setting.SettingValue, setting.ApprovalReference });

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Error.Conflict("Settings.Concurrency", "This setting was changed by someone else. Reload and try again.");
            }

            return ToDto(setting);
        }

        private static SystemSettingDto ToDto(SystemSetting s) => new(
            s.SettingKey, s.SettingValue, s.ValueType, s.Description, s.IsMedicalParameter,
            s.ApprovalReference, s.ApprovedByUserId, s.ApprovedAt, s.UpdatedByUserId, s.UpdatedAt,
            RowVersionConverter.ToBase64(s.RowVersion));

        private static Error NotFound(string key) =>
            Error.NotFound("Settings.NotFound", $"Setting '{key}' was not found.");
    }
}
