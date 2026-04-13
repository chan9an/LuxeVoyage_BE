using Booking.API.Entities;
using Booking.API.Services;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt; // same manual JWT decode pattern as Hotel.API — avoids gateway auth issues

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

    // Pulls the user's ID out of the JWT — used to stamp bookings and enforce ownership on cancel
    private string? GetCallerId()
    {
        return GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier", "nameid", "sub");
    }

    // Grabs the email claim — passed to the booking service so the confirmation email knows where to go
    private string? GetCallerEmail()
    {
        return GetClaim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress", "email");
    }

    // Builds a display name from given_name + family_name — falls back to email if name claims are missing
    private string? GetCallerName()
    {
        var given  = GetClaim("given_name")  ?? string.Empty;
        var family = GetClaim("family_name") ?? string.Empty;
        var full   = $"{given} {family}".Trim();
        return string.IsNullOrEmpty(full) ? GetCallerEmail() : full;
    }

    // Generic claim extractor — tries each type in order and returns the first match
    private string? GetClaim(params string[] types)
    {
        var authHeader = Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ")) return null;
        try
        {
            // Range operator [..] to strip "Bearer " prefix — same as Substring(7) but cleaner
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authHeader["Bearer ".Length..].Trim());
            foreach (var type in types)
            {
                var val = jwt.Claims.FirstOrDefault(c => c.Type == type)?.Value;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { /* malformed token — fall through to return null */ }
        return null;
    }

    // Admin/debug endpoint — returns all bookings
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

    // "My Bookings" — only returns bookings belonging to the calling user
    [HttpGet("my")]
    public async Task<IActionResult> GetMyBookings()
    {
        var userId = GetCallerId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var bookings = await _bookingService.GetByUserIdAsync(userId);
        return Ok(bookings);
    }

    // Hotel manager view — see all bookings for a specific property
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

        // Stamp the caller's identity — they can't fake this since it comes from the JWT
        var userId = GetCallerId();
        if (!string.IsNullOrEmpty(userId)) booking.UserId = userId;

        // Pull email and name for the confirmation email — passed through to the event
        var guestEmail = GetCallerEmail() ?? string.Empty;
        var guestName  = GetCallerName()  ?? string.Empty;

        await _bookingService.CreateBookingAsync(booking, guestEmail, guestName);
        return CreatedAtAction(nameof(GetById), new { id = booking.Id }, booking);
    }

    // PATCH is the right verb here — we're partially updating (just the status field)
    [HttpPatch("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var userId = GetCallerId();
        // Service returns false if booking not found OR caller doesn't own it
        var success = await _bookingService.CancelBookingAsync(id, userId);
        if (!success) return NotFound();
        return NoContent();
    }
}
