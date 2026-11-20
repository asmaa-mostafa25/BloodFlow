using System.ComponentModel.DataAnnotations;
using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Settings
{
    public sealed record SystemSettingDto(
        string Key,
        string Value,
        SettingValueType ValueType,
        string Description,
        bool IsMedicalParameter,
        string? ApprovalReference,
        int? ApprovedByUserId,
        DateTime? ApprovedAt,
        int? UpdatedByUserId,
        DateTime? UpdatedAt,
        string RowVersion);

    public sealed record UpdateSystemSettingRequest
    {
        [Required, StringLength(200)]
        public string Value { get; init; } = string.Empty;

        /// <summary>
        /// Required for medical parameters: a reference to the clinical approval
        /// (e.g. "Supervisor email 2026-11-20" or a document number).
        /// </summary>
        [StringLength(200)]
        public string? ApprovalReference { get; init; }

        [Required]
        public string RowVersion { get; init; } = string.Empty;
    }
}
