namespace Shared.Events;

public interface IBookingStartedEvent
{
    Guid BookingId { get; }
    Guid HotelId { get; }
    Guid RoomId { get; }
    DateTime CheckInDate { get; }
    DateTime CheckOutDate { get; }
    decimal TotalPrice { get; }
    // Display fields for email notification
    string GuestEmail { get; }
    string GuestName { get; }
    string HotelName { get; }
    string RoomName { get; }
    string Location { get; }
    int GuestCount { get; }
    int RoomsBooked { get; }
}
