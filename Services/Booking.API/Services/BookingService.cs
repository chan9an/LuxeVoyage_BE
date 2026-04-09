using Booking.API.Entities;
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
    {
        return await _bookingRepository.GetAllAsync();
    }

    public async Task<BookingEntity?> GetByIdAsync(Guid id)
    {
        return await _bookingRepository.GetByIdAsync(id);
    }

    public async Task CreateBookingAsync(BookingEntity booking)
    {
        booking.Id = Guid.NewGuid();
        booking.Status = Enums.BookingStatus.Pending;
        booking.CreatedAt = DateTime.UtcNow;

        await _bookingRepository.AddAsync(booking);
        await _bookingRepository.SaveChangesAsync();

        _logger.LogInformation("Booking {BookingId} created, publishing start event", booking.Id);

        await _publishEndpoint.Publish<IBookingStartedEvent>(new
        {
            BookingId = booking.Id,
            booking.HotelId,
            booking.RoomId,
            booking.CheckInDate,
            booking.CheckOutDate,
            booking.TotalPrice
        });
    }
}
