using BloodFlow.Domain.Enums;

namespace BloodFlow.Domain.Entities
{
    /// <summary>
    /// A configurable system parameter (e.g. donation interval, shelf life).
    /// Medical parameters can only be changed with an approval reference.
    /// </summary>
    public class SystemSetting
    {
        public required string SettingKey { get; set; }
        public required string SettingValue { get; set; }
        public SettingValueType ValueType { get; set; }
        public required string Description { get; set; }

        public bool IsMedicalParameter { get; set; }
        public string? ApprovalReference { get; set; }
        public int? ApprovedByUserId { get; set; }
        public DateTime? ApprovedAt { get; set; }

        public int? UpdatedByUserId { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
}
