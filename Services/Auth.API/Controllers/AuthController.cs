using System.Security.Claims;
using Auth.API.Application.Interfaces;
using Auth.API.Domain.Entities;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Events;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IJwtTokenGenerator _jwtTokenGenerator;
        private readonly IPublishEndpoint _publishEndpoint;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IJwtTokenGenerator jwtTokenGenerator,
            IPublishEndpoint publishEndpoint)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtTokenGenerator = jwtTokenGenerator;
            _publishEndpoint = publishEndpoint;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FirstName = request.FirstName,
                LastName = request.LastName
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            string assignedRole = request.Role == "HotelManager" ? "HotelManager" : "Customer";
            await _userManager.AddToRoleAsync(user, assignedRole);

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user, roles);

            await _publishEndpoint.Publish<IUserRegisteredEvent>(new
            {
                UserId    = user.Id,
                Email     = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName  = user.LastName ?? string.Empty
            });

            return Ok(new { Token = token, Role = assignedRole });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized("Invalid credentials.");

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user, roles);
            return Ok(new { Token = token, Roles = roles });
        }

        [HttpGet("external-login")]
        public IActionResult ExternalLogin()
        {
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme,
                Url.Action(nameof(ExternalLoginCallback)));
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("external-callback")]
        public async Task<IActionResult> ExternalLoginCallback()
        {
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null) return BadRequest("Error loading external login information.");

            var email = info.Principal.FindFirstValue(ClaimTypes.Email);
            if (string.IsNullOrEmpty(email)) return BadRequest("Google did not provide an email address.");

            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FirstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
                    LastName  = info.Principal.FindFirstValue(ClaimTypes.Surname)   ?? string.Empty
                };

                var createResult = await _userManager.CreateAsync(user);
                if (createResult.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                    await _userManager.AddLoginAsync(user, info);

                    await _publishEndpoint.Publish<IUserRegisteredEvent>(new
                    {
                        UserId    = user.Id,
                        Email     = user.Email ?? string.Empty,
                        FirstName = user.FirstName ?? string.Empty,
                        LastName  = user.LastName ?? string.Empty
                    });
                }
                else return BadRequest(createResult.Errors);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user, roles);
            var primaryRole = roles.FirstOrDefault() ?? "Customer";
            return Redirect($"http://localhost:4200/login?token={token}&role={primaryRole}");
        }
    }

    public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role = "Customer");
    public record LoginRequest(string Email, string Password);
}
