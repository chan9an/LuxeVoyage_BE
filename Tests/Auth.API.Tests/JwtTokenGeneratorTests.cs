using Auth.API.Domain.Entities;
using Auth.API.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Auth.API.Tests;

/*
 * These tests cover JwtTokenGenerator — the single class responsible for minting JWTs in the system.
 * Every test follows the same pattern: call GenerateToken(), crack open the resulting token string
 * using JwtSecurityTokenHandler.ReadJwtToken(), and assert that the payload contains exactly what
 * we expect. We never make an HTTP call or touch a database here — this is pure logic testing.
 */
[TestFixture]
public class JwtTokenGeneratorTests
{
    private JwtTokenGenerator _generator;

    /*
     * [SetUp] runs before every single test method. We build a fresh IConfiguration from an
     * in-memory dictionary so the generator has something to read from — this mirrors exactly
     * what happens in production where the config comes from .env via appsettings.json.
     * Using a short, known secret lets us keep tests deterministic and self-contained.
     */
    [SetUp]
    public void SetUp()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"]        = "TestSecret_MustBe32CharsOrLonger!",
                ["JwtSettings:Issuer"]        = "TestIssuer",
                ["JwtSettings:Audience"]      = "TestAudience",
                ["JwtSettings:ExpiryMinutes"] = "60"
            })
            .Build();

        _generator = new JwtTokenGenerator(config);
    }

    // The most basic sanity check — if this fails, nothing else matters.
    [Test]
    public void GenerateToken_ValidUser_ReturnsNonEmptyString()
    {
        var user  = BuildUser();
        var token = _generator.GenerateToken(user, new[] { "Customer" });

        Assert.That(token, Is.Not.Null.And.Not.Empty);
    }

    /*
     * A JWT is always three base64url-encoded segments separated by dots: header.payload.signature.
     * If we get anything other than 3 parts, the token is malformed and no middleware will accept it.
     */
    [Test]
    public void GenerateToken_TokenIsValidJwt_HasThreeParts()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer" });

        Assert.That(token.Split('.').Length, Is.EqualTo(3));
    }

    // Verifies the email claim is embedded correctly — this is what the frontend reads to show the user's email.
    [Test]
    public void GenerateToken_ContainsCorrectEmail()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer" });
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var email = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;

        Assert.That(email, Is.EqualTo("test@luxevoyage.in"));
    }

    /*
     * The user ID must be in the 'sub' claim. This is the standard JWT claim for subject identity
     * and is what Hotel.API and Booking.API read when they manually decode the token to get the
     * caller's identity for ownership checks.
     */
    [Test]
    public void GenerateToken_ContainsUserId_InSubClaim()
    {
        var user  = BuildUser();
        var token = _generator.GenerateToken(user, new[] { "Customer" });
        var sub   = new JwtSecurityTokenHandler().ReadJwtToken(token)
                        .Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;

        Assert.That(sub, Is.EqualTo(user.Id));
    }

    // Verifies that a single role gets embedded as a ClaimTypes.Role claim — used by [Authorize(Roles="...")] middleware.
    [Test]
    public void GenerateToken_ContainsRoleClaim()
    {
        var token     = _generator.GenerateToken(BuildUser(), new[] { "HotelManager" });
        var roleClaim = new JwtSecurityTokenHandler().ReadJwtToken(token)
                            .Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;

        Assert.That(roleClaim, Is.EqualTo("HotelManager"));
    }

    /*
     * A user could theoretically have multiple roles. We test that all of them end up in the token
     * as separate ClaimTypes.Role entries rather than being concatenated or only the first one being included.
     */
    [Test]
    public void GenerateToken_MultipleRoles_AllRolesPresent()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer", "HotelManager" });
        var roles = new JwtSecurityTokenHandler().ReadJwtToken(token)
                        .Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.That(roles, Contains.Item("Customer"));
        Assert.That(roles, Contains.Item("HotelManager"));
    }

    // The issuer claim must match what the JWT middleware is configured to accept — a mismatch causes a 401.
    [Test]
    public void GenerateToken_HasCorrectIssuer()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer" });
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.That(jwt.Issuer, Is.EqualTo("TestIssuer"));
    }

    // Same story for audience — the middleware validates this too, so it must be exactly right.
    [Test]
    public void GenerateToken_HasCorrectAudience()
    {
        var token    = _generator.GenerateToken(BuildUser(), new[] { "Customer" });
        var audience = new JwtSecurityTokenHandler().ReadJwtToken(token).Audiences.FirstOrDefault();

        Assert.That(audience, Is.EqualTo("TestAudience"));
    }

    // Tokens must expire in the future — a token that's already expired on creation would be useless.
    [Test]
    public void GenerateToken_ExpiryIsInFuture()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer" });
        var jwt   = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.That(jwt.ValidTo, Is.GreaterThan(DateTime.UtcNow));
    }

    /*
     * The Jti (JWT ID) claim is a unique identifier for each token. It's important for future
     * token revocation — if we ever build a token blacklist, we'd store Jti values. This test
     * confirms the Jti is a valid GUID rather than an empty string or some other garbage value.
     */
    [Test]
    public void GenerateToken_ContainsJtiClaim()
    {
        var token = _generator.GenerateToken(BuildUser(), new[] { "Customer" });
        var jti   = new JwtSecurityTokenHandler().ReadJwtToken(token)
                        .Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

        Assert.That(jti, Is.Not.Null.And.Not.Empty);
        Assert.That(Guid.TryParse(jti, out _), Is.True);
    }

    /*
     * Two tokens generated for the same user must have different Jti values. If they were the same,
     * it would mean the GUID generation is broken — and a token blacklist based on Jti would be useless
     * because revoking one token would accidentally revoke all tokens for that user.
     */
    [Test]
    public void GenerateToken_TwoCallsProduceDifferentJti()
    {
        var user   = BuildUser();
        var token1 = _generator.GenerateToken(user, new[] { "Customer" });
        var token2 = _generator.GenerateToken(user, new[] { "Customer" });

        var handler = new JwtSecurityTokenHandler();
        var jti1    = handler.ReadJwtToken(token1).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2    = handler.ReadJwtToken(token2).Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        Assert.That(jti1, Is.Not.EqualTo(jti2));
    }

    // Builds a minimal ApplicationUser with just enough data for the generator to work with.
    private static ApplicationUser BuildUser() => new()
    {
        Id        = Guid.NewGuid().ToString(),
        Email     = "test@luxevoyage.in",
        UserName  = "test@luxevoyage.in",
        FirstName = "Test",
        LastName  = "User"
    };
}
