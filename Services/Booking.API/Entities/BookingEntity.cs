using Booking.API.Enums;

namespace Booking.API.Entities;

public class BookingEntity
{
    public Guid Id { get; set; }
    public Guid HotelId { get; set; }
    public Guid RoomId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalPrice { get; set; }
    public int GuestCount { get; set; } = 1;
    public int RoomsBooked { get; set; } = 1;
    // Denormalized display fields (so we don't need to call Hotel API to show bookings)
    public string HotelName { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string HotelImageUrl { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
