using System;
using System.Collections.Generic;
using System.Text;

namespace BloodFlow.Application.Auth
{
   public record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc
  );
}
