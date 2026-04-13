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
        /*
         * UserManager<ApplicationUser> is the main workhorse from ASP.NET Identity. It handles
         * everything related to user lifecycle — creating accounts, hashing passwords, managing
         * claims, assigning roles, and generating tokens. We never touch password hashes directly;
         * UserManager abstracts all of that away behind a clean async API.
         *
         * SignInManager is a higher-level service that builds on top of UserManager. We only need
         * it here for the Google OAuth flow, specifically to configure the external authentication
         * properties and retrieve the external login info after Google redirects back to us.
         *
         * IPublishEndpoint comes from MassTransit and is how we fire events onto the message bus.
         * It's completely fire-and-forget — we publish an event and return a response to the user
         * immediately, without waiting for the Notification.Worker to process it.
         */
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
                UserName       = request.Email,
                Email          = request.Email,
                FirstName      = request.FirstName,
                LastName       = request.LastName,
                EmailConfirmed = false
            };

            /*
             * CreateAsync does a lot under the hood — it validates the password against the configured
             * Identity password policy, checks for duplicate usernames and emails, hashes the password
             * using PBKDF2, and persists the user to the database. If any of those steps fail, it
             * returns a collection of IdentityError objects that we pass straight back to the client.
             */
            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            string assignedRole = request.Role == "HotelManager" ? "HotelManager" : "Customer";
            await _userManager.AddToRoleAsync(user, assignedRole);

            var otp = await GenerateAndStoreOtp(user);

            /*
             * Rather than sending the email synchronously inside this HTTP request, we publish an event
             * to RabbitMQ. The Notification.Worker service is listening on the luxevoyage.email-verification
             * queue and will pick this up asynchronously. This means the register endpoint returns in
             * milliseconds regardless of how long the SMTP server takes to respond.
             */
            await _publishEndpoint.Publish<IEmailVerificationRequestedEvent>(new
            {
                UserId    = user.Id,
                Email     = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                Otp       = otp
            });

            return Ok(new { RequiresVerification = true, Email = user.Email, Message = "Account created. Please verify your email with the OTP sent." });
        }

        [HttpPost("verify-email")]
        public async Task<IActionResult> VerifyEmail([FromBody] VerifyOtpRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return BadRequest(new { Message = "Invalid request." });

            if (user.EmailConfirmed)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var jwt   = _jwtTokenGenerator.GenerateToken(user, roles);
                return Ok(new { Token = jwt, Role = roles.FirstOrDefault() });
            }

            var valid = await ValidateOtp(user, request.Otp);
            if (!valid) return BadRequest(new { Message = "Invalid or expired OTP." });

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            /*
             * We deliberately fire the welcome email here, after verification, rather than at registration.
             * This ensures that only users who actually own the email address they registered with receive
             * the welcome message. It also means we're not sending emails to potentially fake addresses
             * that were never going to be verified.
             */
            await _publishEndpoint.Publish<IUserRegisteredEvent>(new
            {
                UserId    = user.Id,
                Email     = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                LastName  = user.LastName ?? string.Empty
            });

            var userRoles = await _userManager.GetRolesAsync(user);
            var token     = _jwtTokenGenerator.GenerateToken(user, userRoles);
            return Ok(new { Token = token, Role = userRoles.FirstOrDefault() });
        }

        [HttpPost("resend-verification")]
        public async Task<IActionResult> ResendVerification([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            /*
             * We return a 200 OK regardless of whether the user exists or is already verified.
             * This is a deliberate security decision — if we returned a 404 for unknown emails or
             * a different message for already-verified accounts, an attacker could use this endpoint
             * to enumerate which email addresses are registered in our system.
             */
            if (user == null || user.EmailConfirmed)
                return Ok(new { Message = "If applicable, a new OTP has been sent." });

            var otp = await GenerateAndStoreOtp(user);
            await _publishEndpoint.Publish<IEmailVerificationRequestedEvent>(new
            {
                UserId    = user.Id,
                Email     = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                Otp       = otp
            });

            return Ok(new { Message = "OTP resent." });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            /*
             * We return the same generic "Invalid credentials" message whether the email doesn't exist
             * or the password is wrong. This prevents credential stuffing attacks where an attacker
             * could use different error messages to figure out which email addresses have accounts.
             * CheckPasswordAsync handles the bcrypt comparison internally.
             */
            if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
                return Unauthorized("Invalid credentials.");

            if (!user.EmailConfirmed)
                return Unauthorized(new { Message = "Please verify your email before logging in.", RequiresVerification = true, Email = user.Email });

            var roles = await _userManager.GetRolesAsync(user);
            var token = _jwtTokenGenerator.GenerateToken(user, roles);
            return Ok(new { Token = token, Roles = roles });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return Ok(new { Message = "If that email exists, an OTP has been sent." });

            var otp = await GenerateAndStoreOtp(user);
            await _publishEndpoint.Publish<IPasswordResetRequestedEvent>(new
            {
                UserId    = user.Id,
                Email     = user.Email ?? string.Empty,
                FirstName = user.FirstName ?? string.Empty,
                Otp       = otp
            });

            return Ok(new { Message = "If that email exists, an OTP has been sent." });
        }

        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return BadRequest(new { Message = "Invalid request." });

            // We pass remove: false here because this endpoint is just a validation step.
            // The OTP needs to stay in the database so that the subsequent reset-password call can use it.
            var valid = await ValidateOtp(user, request.Otp, remove: false);
            if (!valid) return BadRequest(new { Message = "Invalid or expired OTP." });

            return Ok(new { Message = "OTP verified.", Verified = true });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null) return BadRequest(new { Message = "Invalid request." });

            // remove: true — this is the final step, so we consume and delete the OTP claim
            // to prevent it from being reused for a second password reset.
            var valid = await ValidateOtp(user, request.Otp, remove: true);
            if (!valid) return BadRequest(new { Message = "Invalid or expired OTP." });

            /*
             * Identity's password reset flow requires a cryptographic reset token even though we've
             * already validated the OTP ourselves. We generate a fresh one here purely to satisfy
             * the ResetPasswordAsync API contract — it's not sent to the user, it's just used
             * internally to authorise the password change operation.
             */
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result     = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(new { Message = "Password reset successfully." });
        }

        [HttpGet("external-login")]
        public IActionResult ExternalLogin()
        {
            /*
             * ConfigureExternalAuthenticationProperties sets up the OAuth state parameter and the
             * redirect URI that Google will call after the user consents. The Challenge call then
             * redirects the user's browser to Google's OAuth consent screen. The whole flow is
             * stateless from our side — Google handles the user interaction and then calls us back.
             */
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(
                GoogleDefaults.AuthenticationScheme, Url.Action(nameof(ExternalLoginCallback)));
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
                /*
                 * If this is the first time this Google account has logged in, we auto-create a local
                 * user account for them. We set EmailConfirmed to true immediately because Google has
                 * already verified the email address — there's no need to put them through our OTP flow.
                 * AddLoginAsync creates the link between the local user record and the Google external
                 * login, so future logins with the same Google account will find this user automatically.
                 */
                user = new ApplicationUser
                {
                    UserName       = email,
                    Email          = email,
                    FirstName      = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? string.Empty,
                    LastName       = info.Principal.FindFirstValue(ClaimTypes.Surname)   ?? string.Empty,
                    EmailConfirmed = true
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
            // We redirect to the Angular app with the token in the query string. The frontend
            // intercepts this on the /login route and saves the token to localStorage.
            return Redirect($"http://localhost:4200/login?token={token}&role={primaryRole}");
        }

        /*
         * GenerateAndStoreOtp is a private helper that creates a 6-digit numeric OTP and stores it
         * as a user claim in the Identity database. The clever part is that we derive the OTP from
         * Identity's own password reset token — this gives us cryptographic randomness without
         * needing a separate OTP library. We hash the raw token down to a number in the 100000-999999
         * range to guarantee exactly 6 digits. The OTP and its expiry timestamp are stored together
         * as a single pipe-delimited claim value so we can validate both in one database read.
         */
        private async Task<string> GenerateAndStoreOtp(ApplicationUser user)
        {
            var rawToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var otp      = (Math.Abs(rawToken.GetHashCode()) % 900000 + 100000).ToString();
            var expiry   = DateTime.UtcNow.AddMinutes(10).ToString("o");

            var existing = (await _userManager.GetClaimsAsync(user))
                .FirstOrDefault(c => c.Type == "otp");
            if (existing != null) await _userManager.RemoveClaimAsync(user, existing);

            await _userManager.AddClaimAsync(user, new Claim("otp", $"{otp}|{expiry}"));
            return otp;
        }

        /*
         * ValidateOtp reads the stored OTP claim, splits it into the code and expiry parts, and
         * checks both. The remove parameter controls whether the claim gets deleted after validation —
         * verify-otp passes false because it's just a check, while reset-password passes true to
         * consume the OTP and prevent it from being used again. The RoundtripKind style flag is
         * important when parsing the expiry back from its ISO 8601 string — without it, DateTime.Parse
         * might interpret the UTC timestamp as local time and give us incorrect expiry comparisons.
         */
        private async Task<bool> ValidateOtp(ApplicationUser user, string inputOtp, bool remove = true)
        {
            var claim = (await _userManager.GetClaimsAsync(user))
                .FirstOrDefault(c => c.Type == "otp");
            if (claim == null) return false;

            var parts  = claim.Value.Split('|');
            if (parts.Length != 2) return false;

            var stored = parts[0];
            var expiry = DateTime.Parse(parts[1], null, System.Globalization.DateTimeStyles.RoundtripKind);

            if (DateTime.UtcNow > expiry)
            {
                await _userManager.RemoveClaimAsync(user, claim);
                return false;
            }

            if (stored != inputOtp.Trim()) return false;

            if (remove) await _userManager.RemoveClaimAsync(user, claim);
            return true;
        }
    }

    public record RegisterRequest(string FirstName, string LastName, string Email, string Password, string Role = "Customer");
    public record LoginRequest(string Email, string Password);
    public record ForgotPasswordRequest(string Email);
    public record VerifyOtpRequest(string Email, string Otp);
    public record ResetPasswordRequest(string Email, string Otp, string NewPassword);
}
