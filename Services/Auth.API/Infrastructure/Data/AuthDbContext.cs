using Auth.API.Domain.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth.API.Infrastructure.Data
{
    /*
     * IdentityDbContext<ApplicationUser> is a specialised EF Core DbContext that already knows about
     * all the Identity tables — AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, and so on.
     * We don't need to define those DbSets ourselves; the base class handles all of that scaffolding.
     * Our only job here is to pass in our custom ApplicationUser type so Identity uses our extended
     * user entity instead of the plain IdentityUser.
     */
    public class AuthDbContext : IdentityDbContext<ApplicationUser>
    {
        public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // We must call base.OnModelCreating first because Identity uses this method to configure
            // all its table relationships and indexes. If we skip it or call it after our own config,
            // things break in subtle and confusing ways at runtime.
            base.OnModelCreating(modelBuilder);
        }
    }
}
