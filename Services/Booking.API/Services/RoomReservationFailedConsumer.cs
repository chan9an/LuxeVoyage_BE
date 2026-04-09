using MassTransit;
using Shared.Events;
using Booking.API.Repositories;
using Booking.API.Entities;
using Booking.API.Enums;

namespace Booking.API.Services;

public class RoomReservationFailedConsumer : IConsumer<IRoomReservationFailedEvent>
{
    private readonly IRepository<BookingEntity> _bookingRepository;
    private readonly ILogger<RoomReservationFailedConsumer> _logger;

    public RoomReservationFailedConsumer(IRepository<BookingEntity> bookingRepository, ILogger<RoomReservationFailedConsumer> logger)
    {
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IRoomReservationFailedEvent> context)
    {
        var message = context.Message;
        _logger.LogWarning("Room reservation failed for booking {BookingId}: {Reason}", message.BookingId, message.Reason);

        var booking = await _bookingRepository.GetByIdAsync(message.BookingId);
        if (booking != null)
        {
            booking.Status = BookingStatus.Failed;
            await _bookingRepository.UpdateAsync(booking);
            await _bookingRepository.SaveChangesAsync();
            _logger.LogWarning("Booking {BookingId} marked as failed", message.BookingId);
        }
    }
}
