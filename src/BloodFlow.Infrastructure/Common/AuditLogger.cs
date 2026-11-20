using System.Text.Json;
using System.Text.Json.Serialization;
using BloodFlow.Application.Common;
using BloodFlow.Domain.Entities;
using BloodFlow.Domain.Enums;
using BloodFlow.Infrastructure.Persistence;

namespace BloodFlow.Infrastructure.Common
{
    /// <summary>
    /// Adds an AuditLog row to the SAME DbContext as the change being audited, without saving.
    /// The caller's SaveChanges then writes the change and its audit row together:
    /// either both are saved or neither is.
    /// </summary>
    internal sealed class AuditLogger
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly AppDbContext _db;
        private readonly ICurrentUser _currentUser;

        public AuditLogger(AppDbContext db, ICurrentUser currentUser)
        {
            _db = db;
            _currentUser = currentUser;
        }

        /// <param name="actorUserId">
        /// Overrides the signed-in user. Used for self-registration, where nobody is signed in
        /// yet and the actor is the new user.
        /// </param>
        public void Add(
            string action,
            string entityType,
            string? entityId,
            object? oldValues = null,
            object? newValues = null,
            string? reason = null,
            int? organizationId = null,
            int? actorUserId = null,
            string? actorRole = null)
        {
            var userId = actorUserId ?? _currentUser.UserId;

            _db.AuditLogs.Add(new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                ActorType = userId.HasValue ? ActorType.User : ActorType.System,
                ActorUserId = userId,
                ActorRole = actorRole ?? _currentUser.Role,
                OrganizationId = organizationId,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues, JsonOptions),
                NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues, JsonOptions),
                Reason = reason,
                IpAddress = _currentUser.IpAddress
            });
        }
    }
}
