using Booking.API.Entities;
using Booking.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;

namespace Booking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingController : ControllerBase
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    private string? GetCallerId()
    {
        return GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "nameid", "sub");
    }

    private string? GetCallerEmail()
    {
        return GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "email");
    }

    private string? GetCallerName()
    {
        var given  = GetClaim("given_name")  ?? string.Empty;
        var family = GetClaim("family_name") ?? string.Empty;
        var full   = $"{given} {family}".Trim();
        return string.IsNullOrEmpty(full) ? GetCallerEmail() : full;
    }

    private string? GetClaim(params string[] types)
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ")) return null;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authHeader["Bearer ".Length..].Trim());
            foreach (var type in types)
            {
                var val = jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var bookings = await _bookingService.GetAllAsync();
        return Ok(bookings);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var booking = await _bookingService.GetByIdAsync(id);
        if (booking == null) return NotFound();
        return Ok(booking);
    }

    // Get bookings for the logged-in user
    [HttpGet("my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = GetCallerId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var bookings = await _bookingService.GetByUserIdAsync(userId);
        return Ok(bookings);
    }

    // Get bookings for a specific hotel (for hotel managers)
    [HttpGet("hotel/{hotelId}")]
    public async Task<IActionResult> GetByHotel(Guid hotelId)
    {
        var bookings = await _bookingService.GetByHotelIdAsync(hotelId);
        return Ok(bookings);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookingEntity booking)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = GetCallerId();
        if (!string.IsNullOrEmpty(userId)) booking.UserId = userId;

        var guestEmail = GetCallerEmail() ?? string.Empty;
        var guestName  = GetCallerName()  ?? string.Empty;

        await _bookingService.CreateBookingAsync(booking, guestEmail, guestName);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetCallerId();
        var success = await _bookingService.CancelBookingAsync(id, userId);
        if (!success) return NotFound();
        return NoContent();
    }
}
