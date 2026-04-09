namespace Shared.Events;

public interface IBookingStartedEvent
{
    Guid BookingId { get; }
    Guid HotelId { get; }
    Guid RoomId { get; }
    DateTime CheckInDate { get; }
    DateTime CheckOutDate { get; }
    decimal TotalPrice { get; }
}
