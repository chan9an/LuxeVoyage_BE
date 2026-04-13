using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace Auth.API.Domain.Entities
{
    /*
     * We extend IdentityUser rather than building a user entity from scratch because ASP.NET Identity
     * already handles everything we'd have to write ourselves — password hashing, lockout policies,
     * email confirmation flags, security stamps, and the full claims infrastructure. By inheriting from it,
     * we get all of that for free and just bolt on the two extra fields our app actually needs.
     */
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;
    }
}
