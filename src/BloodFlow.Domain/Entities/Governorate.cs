using System;

using System.Collections.Generic;
namespace BloodFlow.Domain.Entities
{
  public class Governorate
    {
        //shorts>> smallint 2 bytes
        public short Id { get; set; }
        public required string Name { get; set; }
        public ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    }
}
