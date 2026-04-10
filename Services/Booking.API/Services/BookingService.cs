using Booking.API.Entities;
using Booking.API.Enums;
using Booking.API.Repositories;
using MassTransit;
using Shared.Events;

namespace Booking.API.Services;

public class BookingService : IBookingService
{
    private readonly IRepository<BookingEntity> _bookingRepository;
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
        booking.Status = BookingStatus.Pending;
        booking.CreatedAt = DateTime.UtcNow;

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} created, publishing event", booking.Id);

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
        // Only the owner can cancel (or admin with no userId check)
        if (!string.IsNullOrEmpty(userId) && booking.UserId != userId) return false;

        booking.Status = BookingStatus.Cancelled;
        await _bookingRepository.UpdateAsync(booking);
        await _bookingRepository.SaveChangesAsync();
        return true;
    }
}
