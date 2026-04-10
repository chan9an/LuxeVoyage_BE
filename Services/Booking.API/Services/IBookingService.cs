using Booking.API.Entities;

namespace Booking.API.Services;

public interface IBookingService
{
    Task<IEnumerable<BookingEntity>> GetAllAsync();
    Task<BookingEntity?> GetByIdAsync(Guid id);
    Task<IEnumerable<BookingEntity>> GetByUserIdAsync(string userId);
    Task<IEnumerable<BookingEntity>> GetByHotelIdAsync(Guid hotelId);
    Task CreateBookingAsync(BookingEntity booking, string guestEmail, string guestName);
    Task<bool> CancelBookingAsync(Guid id, string? userId);
}
