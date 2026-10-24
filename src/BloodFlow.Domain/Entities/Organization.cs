using BloodFlow.Domain.Enums;
using System;


namespace BloodFlow.Domain.Entities
{
   public class Organization
    {
        public int Id { get; set; }
        public OrganizationType Type { get; set; }
        public required string Name { get; set; } 
        public required string CityName { get; set; }
        public short GovernorateId { get; set; }
        public Governorate Governorate { get; set; }=null!;
        public OrganizationStatus Status { get; set; } = OrganizationStatus.Active;
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
    }
    }

