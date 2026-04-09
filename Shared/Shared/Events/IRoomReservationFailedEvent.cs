namespace Shared.Events;

public interface IRoomReservationFailedEvent
{
    Guid BookingId { get; }
    string Reason { get; }
}
