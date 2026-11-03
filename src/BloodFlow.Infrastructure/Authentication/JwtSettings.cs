using System;
using System.Collections.Generic;
using System.Text;

namespace BloodFlow.Infrastructure.Authentication
{
  public class JwtSettings
    {
        public const string SectionName = "Jwt";
        public string Issuer { get; set; }=null!;
        public string Audience { get; set; } = null!;
        public string Key { get; set; } = null!;
        public int ExpiryMinutes { get; set; } 
    }
}
