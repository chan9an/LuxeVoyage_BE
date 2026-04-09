using MassTransit;
using Shared.Events;
using Hotel.API.Repositories;
using Hotel.API.Entities;

namespace Hotel.API.Services;

public class BookingStartedConsumer : IConsumer<IBookingStartedEvent>
{
    private readonly IRepository<Room> _roomRepository;
    private readonly ILogger<BookingStartedConsumer> _logger;

    public BookingStartedConsumer(IRepository<Room> roomRepository, ILogger<BookingStartedConsumer> logger)
    {
        _roomRepository = roomRepository;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<IBookingStartedEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing booking request for Hotel {HotelId}, Room {RoomId}", message.HotelId, message.RoomId);

        var room = await _roomRepository.GetByIdAsync(message.RoomId);

        if (room != null && room.IsAvailable)
        {
            // Reserve the room
            room.IsAvailable = false;
            _roomRepository.Update(room);
            await _roomRepository.SaveChangesAsync();

            _logger.LogInformation("Room {RoomId} reserved successfully", message.RoomId);

            await context.Publish<IRoomReservedEvent>(new
            {
                message.BookingId,
                message.HotelId,
                message.RoomId,
                message.TotalPrice
            });
        }
        else
        {
            _logger.LogWarning("Room {RoomId} is not available or not found", message.RoomId);

            await context.Publish<IRoomReservationFailedEvent>(new
            {
                message.BookingId,
                Reason = "Room is not available or does not exist."
            });
        }
    }
}
