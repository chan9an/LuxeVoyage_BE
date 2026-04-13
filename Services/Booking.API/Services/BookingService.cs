using Booking.API.Entities;
using Booking.API.Enums;
using Booking.API.Repositories;
using MassTransit;
using Shared.Events;

namespace Booking.API.Services;

public class BookingService : IBookingService
{
    private readonly IRepository<BookingEntity> _bookingRepository;
    /*
     * IPublishEndpoint is MassTransit's abstraction for publishing messages to the message bus.
     * By depending on this interface rather than a concrete RabbitMQ client, we keep the service
     * completely decoupled from the transport layer. If we ever needed to switch from RabbitMQ to
     * Azure Service Bus or another broker, we'd only change the Program.cs configuration — this
     * service class wouldn't need to change at all.
     */
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<BookingService> _logger;

    public BookingService(IRepository<BookingEntity> bookingRepository, IPublishEndpoint publishEndpoint, ILogger<BookingService> logger)
    {
        _bookingRepository = bookingRepository;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task<IEnumerable<BookingEntity>> GetAllAsync()
        => await _bookingRepository.GetAllAsync();

    public async Task<BookingEntity?> GetByIdAsync(Guid id)
        => await _bookingRepository.GetByIdAsync(id);

    public async Task<IEnumerable<BookingEntity>> GetByUserIdAsync(string userId)
        => await _bookingRepository.FindAsync(b => b.UserId == userId);

    public async Task<IEnumerable<BookingEntity>> GetByHotelIdAsync(Guid hotelId)
        => await _bookingRepository.FindAsync(b => b.HotelId == hotelId);

    public async Task CreateBookingAsync(BookingEntity booking, string guestEmail, string guestName)
    {
        booking.Id = Guid.NewGuid();
        // Bookings start as Pending. In a full implementation, a saga or the RoomReservedConsumer
        // would flip this to Confirmed once the room reservation is actually secured.
        booking.Status = BookingStatus.Pending;
        booking.CreatedAt = DateTime.UtcNow;

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} created, publishing event", booking.Id);

        /*
         * After saving the booking to the database, we publish an IBookingStartedEvent to RabbitMQ.
         * The Notification.Worker is listening on the luxevoyage.booking-started queue and will
         * consume this event to send the confirmation email. We include denormalized display fields
         * like HotelName, RoomName, and Location directly in the event payload so the worker can
         * build the email without needing to make a separate HTTP call back to Hotel.API. This is
         * a deliberate architectural choice — it makes the notification flow self-contained and
         * resilient to Hotel.API being temporarily unavailable.
         */
        await _publishEndpoint.Publish<IBookingStartedEvent>(new
        {
            BookingId    = booking.Id,
            booking.HotelId,
            booking.RoomId,
            booking.CheckInDate,
            booking.CheckOutDate,
            booking.TotalPrice,
            GuestEmail   = guestEmail,
            GuestName    = guestName,
            HotelName    = booking.HotelName,
            RoomName     = booking.RoomName,
            Location     = booking.Location,
            GuestCount   = booking.GuestCount,
            RoomsBooked  = booking.RoomsBooked
        });
    }

    public async Task<bool> CancelBookingAsync(Guid id, string? userId)
    {
        var booking = await _bookingRepository.GetByIdAsync(id);
        if (booking == null) return false;

        // When userId is null it means the call came from an admin or system context, so we skip
        // the ownership check. When it's provided, we enforce that only the booking owner can cancel.
        if (!string.IsNullOrEmpty(userId) && booking.UserId != userId) return false;

        booking.Status = BookingStatus.Cancelled;
        await _bookingRepository.UpdateAsync(booking);
        await _bookingRepository.SaveChangesAsync();
        return true;
    }
}
