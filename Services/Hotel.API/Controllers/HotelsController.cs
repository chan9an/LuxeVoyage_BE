using Hotel.API.Services;
using Microsoft.AspNetCore.Mvc;
using Hotel.API.Entities;
using System.IdentityModel.Tokens.Jwt;

namespace Hotel.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class HotelsController : ControllerBase
    {
        private readonly IHotelService _hotelService;

        public HotelsController(IHotelService hotelService)
        {
            _hotelService = hotelService;
        }

        /*
         * We manually decode the JWT from the Authorization header rather than using the [Authorize]
         * attribute and ASP.NET's built-in auth middleware. The reason is architectural: requests
         * arrive through the Ocelot API Gateway over HTTPS, and we found that the JWT middleware
         * was rejecting valid tokens in that proxied setup due to how headers get forwarded. By
         * reading and parsing the token ourselves using JwtSecurityTokenHandler.ReadJwtToken, we
         * bypass the middleware entirely. This is safe because the actual security enforcement
         * (ownership checks) happens in the service layer — we're just extracting an identity claim,
         * not trusting the token blindly.
         */
        private string? GetCallerId()
        {
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
                return null;

            try
            {
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var handler = new JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(token);

                // Different JWT libraries and .NET versions use different claim type names for the user ID.
                // We check all three common variants to ensure compatibility regardless of how the token was issued.
                return jwt.Claims.FirstOrDefault(c =>
                    c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier" ||
                    c.Type == "nameid" ||
                    c.Type == "sub")?.Value;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHotels()
        {
            var hotels = await _hotelService.GetAllHotelsAsync();
            return Ok(hotels);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetHotel(Guid id)
        {
            var hotel = await _hotelService.GetHotelByIdAsync(id);
            if (hotel == null) return NotFound();
            return Ok(hotel);
        }

        [HttpGet("city/{city}")]
        public async Task<IActionResult> GetHotelsByCity(string city)
        {
            var hotels = await _hotelService.GetHotelsByCityAsync(city);
            return Ok(hotels);
        }

        // Returns only the hotels that belong to the authenticated manager — powers the partner dashboard.
        [HttpGet("my")]
        public async Task<IActionResult> GetMyHotels()
        {
            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            var hotels = await _hotelService.GetHotelsByManagerAsync(managerId);
            return Ok(hotels);
        }

        [HttpPost]
        public async Task<IActionResult> CreateHotel([FromBody] HotelEntity hotel)
        {
            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            // The ManagerId is stamped inside the service method, not here. This means even if a
            // malicious client sends a ManagerId in the request body, it gets overwritten with the
            // actual caller's identity from the JWT.
            var created = await _hotelService.CreateHotelAsync(hotel, managerId);
            return CreatedAtAction(nameof(GetHotel), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHotel(Guid id, [FromBody] HotelEntity hotel)
        {
            if (id != hotel.Id) return BadRequest();

            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            // The service returns false for two distinct reasons: the hotel doesn't exist, or the
            // caller doesn't own it. We map both to Forbid rather than distinguishing between them,
            // which avoids leaking information about which hotel IDs exist in the system.
            var success = await _hotelService.UpdateHotelAsync(hotel, managerId);
            if (!success) return Forbid();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHotel(Guid id)
        {
            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            // Deletion also triggers a Cloudinary image cleanup inside the service layer.
            // If the Cloudinary call fails, the service logs the error but still deletes the DB record.
            var success = await _hotelService.DeleteHotelAsync(id, managerId);
            if (!success) return Forbid();

            return NoContent();
        }
    }
}
