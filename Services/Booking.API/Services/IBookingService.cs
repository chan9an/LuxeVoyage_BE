

using Booking.API.Entities;

namespace Booking.API.Services;

public interface IBookingService
{
    Task<IEnumerable<BookingEntity>> GetAllAsync();
    Task<BookingEntity?> GetByIdAsync(Guid id);
    Task CreateBookingAsync(BookingEntity booking);
}
