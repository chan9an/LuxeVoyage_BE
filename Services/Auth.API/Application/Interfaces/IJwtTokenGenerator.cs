using Auth.API.Domain.Entities;
using System.Collections.Generic;

namespace Auth.API.Application.Interfaces
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(ApplicationUser user, IList<string> roles);
    }
}
   