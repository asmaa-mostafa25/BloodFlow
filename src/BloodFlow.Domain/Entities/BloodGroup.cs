using System;


namespace BloodFlow.Domain.Entities
{
public class BloodGroup
    {
        //bytes>> tinyint 1 byte
        public byte Id { get; set; }
       public required string Code { get; set; }
    }
}
