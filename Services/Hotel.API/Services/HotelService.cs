using Hotel.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Hotel.API.Data;
using Hotel.API.Entities;

namespace Hotel.API.Services
{
    public class HotelService : IHotelService
    {
        private readonly IRepository<HotelEntity> _hotelRepository;
        private readonly HotelDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public HotelService(IRepository<HotelEntity> hotelRepository, HotelDbContext context, ICloudinaryService cloudinaryService)
        {
            _hotelRepository = hotelRepository;
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        public async Task<IEnumerable<HotelEntity>> GetAllHotelsAsync()
            => await _context.Hotels
                .Include(h => h.RoomTypes)
                .Include(h => h.Rooms)
                .ToListAsync();

        public async Task<HotelEntity?> GetHotelByIdAsync(Guid id)
            => await _context.Hotels
                .Include(h => h.RoomTypes)
                .Include(h => h.Rooms)
                .FirstOrDefaultAsync(h => h.Id == id);

        public async Task<IEnumerable<HotelEntity>> GetHotelsByManagerAsync(string managerId)
            => await _context.Hotels
                .Where(h => h.ManagerId == managerId)
                .Include(h => h.RoomTypes)
                .Include(h => h.Rooms)
                .ToListAsync();

        public async Task<HotelEntity> CreateHotelAsync(HotelEntity hotel, string managerId)
        {
            hotel.Id = Guid.NewGuid();
            hotel.ManagerId = managerId;
            await _hotelRepository.AddAsync(hotel);
            await _hotelRepository.SaveChangesAsync();
            return hotel;
        }

        public async Task<bool> UpdateHotelAsync(HotelEntity hotel, string managerId)
        {
            var existing = await _context.Hotels.FindAsync(hotel.Id);
            if (existing == null) return false;

            // Ownership check
            if (existing.ManagerId != managerId) return false;

            // Update only the scalar fields on the already-tracked entity
            existing.Name          = hotel.Name;
            existing.Location      = hotel.Location;
            existing.Type          = hotel.Type;
            existing.PricePerNight = hotel.PricePerNight;
            existing.Currency      = hotel.Currency;
            existing.ImageUrl      = hotel.ImageUrl;
            existing.Description   = hotel.Description;
            existing.Amenities     = hotel.Amenities;
            // ManagerId, Rating, ReviewCount intentionally not overwritten by manager

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteHotelAsync(Guid id, string managerId)
        {
            var hotel = await _hotelRepository.GetByIdAsync(id);
            if (hotel == null) return false;

            // Ownership check
            if (hotel.ManagerId != managerId) return false;

            if (!string.IsNullOrEmpty(hotel.ImageUrl))
                await _cloudinaryService.DeleteImageAsync(hotel.ImageUrl);

            _hotelRepository.Remove(hotel);
            await _hotelRepository.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<HotelEntity>> GetHotelsByCityAsync(string city)
            => await _context.Hotels
                .Where(h => h.Location.ToLower().Contains(city.ToLower()))
                .Include(h => h.RoomTypes)
                .Include(h => h.Rooms)
                .ToListAsync();
    }
}
