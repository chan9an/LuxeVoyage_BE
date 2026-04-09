using MassTransit;
using Shared.Events;
using Booking.API.Repositories;
using Booking.API.Entities;
using Booking.API.Enums;

namespace Booking.API.Services;

public class RoomReservedConsumer : IConsumer<IRoomReservedEvent>
{
    private readonly IRepository<BookingEntity> _bookingRepository;
    private readonly ILogger<RoomReservedConsumer> _logger;

    public RoomReservedConsumer(IRepository<BookingEntity> bookingRepository, ILogger<RoomReservedConsumer> logger)
    {
        _bookingRepository = bookingRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IRoomReservedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Room reserved for booking {BookingId}, confirming booking", message.BookingId);

        var booking = await _bookingRepository.GetByIdAsync(message.BookingId);
        if (booking != null)
        {
            booking.Status = BookingStatus.Confirmed;
            await _bookingRepository.UpdateAsync(booking);
            await _bookingRepository.SaveChangesAsync();
            _logger.LogInformation("Booking {BookingId} confirmed", message.BookingId);
        }
    }
}
