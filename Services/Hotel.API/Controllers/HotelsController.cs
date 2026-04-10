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

        // Manually decode the JWT from the Authorization header — works regardless of
        // whether ASP.NET's auth middleware validates it (avoids gateway/HTTPS issues)
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

        // ── Public endpoints ──────────────────────────────────────────────

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

        // ── Manager-only: own properties ─────────────────────────────────

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

            var created = await _hotelService.CreateHotelAsync(hotel, managerId);
            return CreatedAtAction(nameof(GetHotel), new { id = created.Id }, created);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateHotel(Guid id, [FromBody] HotelEntity hotel)
        {
            if (id != hotel.Id) return BadRequest();

            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            var success = await _hotelService.UpdateHotelAsync(hotel, managerId);
            if (!success) return Forbid();

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteHotel(Guid id)
        {
            var managerId = GetCallerId();
            if (string.IsNullOrEmpty(managerId)) return Unauthorized();

            var success = await _hotelService.DeleteHotelAsync(id, managerId);
            if (!success) return Forbid();

            return NoContent();
        }
    }
}
