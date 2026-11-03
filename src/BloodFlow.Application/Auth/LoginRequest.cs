using System;
using System.Collections.Generic;
using System.Text;

namespace BloodFlow.Application.Auth
{
  public record LoginRequest(
    string Email,
    string Password
  );


}
