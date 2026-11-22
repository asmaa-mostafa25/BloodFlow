using System.ComponentModel.DataAnnotations;

namespace BloodFlow.Application.Common
{
    /// <summary>Body for actions that must explain why (reject, deactivate, cancel...).</summary>
    public sealed record ReasonRequest
    {
        [Required, StringLength(500, MinimumLength = 3)]
        public string Reason { get; init; } = string.Empty;
    }
}
