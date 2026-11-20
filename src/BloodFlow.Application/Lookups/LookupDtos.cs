using BloodFlow.Domain.Enums;

namespace BloodFlow.Application.Lookups
{
    public sealed record GovernorateDto(short Id, string Name);

    public sealed record BloodGroupDto(byte Id, string Code);

    /// <summary>Short organization info for dropdowns (e.g. the staff registration form).</summary>
    public sealed record OrganizationLookupDto(int Id, string Name, OrganizationType Type, short GovernorateId, string CityName);
}
