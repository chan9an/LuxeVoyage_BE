namespace Shared.Events;

public interface IRoomReservedEvent
{
    Guid BookingId { get; }
    Guid HotelId { get; }
    Guid RoomId { get; }
    decimal TotalPrice { get; }
}
