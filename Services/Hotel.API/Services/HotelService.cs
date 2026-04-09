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

        public HotelService(IRepository<HotelEntity> hotelRepository, HotelDbContext context)
        {
            _hotelRepository = hotelRepository;
            _context = context;
        }

        public async Task<IEnumerable<HotelEntity>> GetAllHotelsAsync()
        {
            return await _hotelRepository.GetAllAsync();
        }

        public async Task<HotelEntity?> GetHotelByIdAsync(Guid id)
        {
            return await _hotelRepository.GetByIdAsync(id);
        }

        public async Task<HotelEntity> CreateHotelAsync(HotelEntity hotel)
        {
            hotel.Id = Guid.NewGuid();
            await _hotelRepository.AddAsync(hotel);
            await _hotelRepository.SaveChangesAsync();
            return hotel;
        }

        public async Task UpdateHotelAsync(HotelEntity hotel)
        {
            _hotelRepository.Update(hotel);
            await _hotelRepository.SaveChangesAsync();
        }

        public async Task DeleteHotelAsync(Guid id)
        {
            var hotel = await _hotelRepository.GetByIdAsync(id);
            if (hotel != null)
            {
                _hotelRepository.Remove(hotel);
                await _hotelRepository.SaveChangesAsync();
            }
        }

        public async Task<IEnumerable<HotelEntity>> GetHotelsByCityAsync(string city)
        {
            return await _context.Hotels
                .Where(h => h.Location.ToLower().Contains(city.ToLower()))
                .ToListAsync();
        }
    }
}
