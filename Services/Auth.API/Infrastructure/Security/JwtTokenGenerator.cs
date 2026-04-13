using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Auth.API.Application.Interfaces;
using Auth.API.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Auth.API.Infrastructure.Security
{
    /*
     * This class is the single place in the entire system responsible for minting JWTs.
     * We keep it behind an interface (IJwtTokenGenerator) so that if we ever need to swap
     * the signing algorithm or add new claims, we only change this one file and nothing else
     * in the codebase needs to know about it. It's a classic example of the Dependency Inversion
     * principle paying off in a microservices context.
     */
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IConfiguration _configuration;

        public JwtTokenGenerator(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GenerateToken(ApplicationUser user, IList<string> roles)
        {
            var secret = _configuration["JwtSettings:Secret"];
            var issuer = _configuration["JwtSettings:Issuer"];
            var audience = _configuration["JwtSettings:Audience"];
            var expiryMinutes = int.Parse(_configuration["JwtSettings:ExpiryMinutes"] ?? "60");

            /*
             * SymmetricSecurityKey takes our plain-text secret and wraps it in a cryptographic key object.
             * We then pair it with HMAC-SHA256 to create the signing credentials — this is the algorithm
             * that will be used to sign the token's signature segment, which is what prevents tampering.
             */
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret!));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            /*
             * The claims list is the payload of the JWT — the data that gets embedded inside the token
             * and can be read by any service that receives it. We include both ClaimTypes.NameIdentifier
             * and JwtRegisteredClaimNames.Sub because different parts of the .NET ecosystem look for
             * the user ID under different claim type names. Including both ensures compatibility
             * regardless of which library or middleware is doing the reading downstream.
             */
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            /*
             * JwtSecurityToken assembles all the pieces — issuer, audience, claims, expiry, and signing
             * credentials — into a structured token object. The JwtSecurityTokenHandler then serialises
             * that into the familiar three-part base64url string (header.payload.signature) that gets
             * sent to the client and stored in localStorage on the Angular side.
             */
            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
