using Hotel.API.Repositories;
using Microsoft.EntityFrameworkCore;
using Hotel.API.Data;
using Hotel.API.Entities;

namespace Hotel.API.Services
{
    public class HotelService : IHotelService
    {
        private readonly IRepository<HotelEntity> _hotelRepository;
        /*
         * We inject the DbContext directly alongside the generic repository because the repository
         * pattern we're using doesn't support eager loading with .Include(). For simple CRUD operations
         * the repository is fine, but for queries that need to load related entities (RoomTypes, Rooms)
         * we go straight to the context. This is a pragmatic trade-off — a fully generic repository
         * that supports arbitrary Include chains gets complicated fast, and for a project of this size
         * the direct context access is cleaner and more readable.
         */
        private readonly HotelDbContext _context;
        private readonly ICloudinaryService _cloudinaryService;

        public HotelService(IRepository<HotelEntity> hotelRepository, HotelDbContext context, ICloudinaryService cloudinaryService)
        {
            _hotelRepository = hotelRepository;
            _context = context;
            _cloudinaryService = cloudinaryService;
        }

        // We always include RoomTypes and Rooms in hotel queries because the frontend needs them
        // to display availability, pricing tiers, and the booking capacity validation logic.
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
            // Stamping the ManagerId here in the service layer rather than trusting the incoming
            // request body is a key security decision. It means the ownership of a hotel is always
            // derived from the authenticated user's JWT, never from client-supplied data.
            hotel.ManagerId = managerId;
            await _hotelRepository.AddAsync(hotel);
            await _hotelRepository.SaveChangesAsync();
            return hotel;
        }

        public async Task<bool> UpdateHotelAsync(HotelEntity hotel, string managerId)
        {
            /*
             * We use FindAsync here rather than a LINQ query because FindAsync checks the EF identity
             * cache first — if this entity was already loaded earlier in the same request, we get the
             * cached instance back without hitting the database again. The important consequence is that
             * the returned entity is already being tracked by the change tracker.
             */
            var existing = await _context.Hotels.FindAsync(hotel.Id);
            if (existing == null) return false;

            if (existing.ManagerId != managerId) return false;

            /*
             * We update the scalar fields directly on the already-tracked entity rather than calling
             * _repo.Update(hotel) with the incoming detached entity. The reason is that EF Core throws
             * an InvalidOperationException if you try to attach a new entity instance with the same
             * primary key as one that's already being tracked in the same DbContext scope. By mutating
             * the tracked instance in place, we avoid that conflict entirely and EF's change tracker
             * automatically detects which fields changed and generates a minimal UPDATE statement.
             */
            existing.Name          = hotel.Name;
            existing.Location      = hotel.Location;
            existing.Type          = hotel.Type;
            existing.PricePerNight = hotel.PricePerNight;
            existing.Currency      = hotel.Currency;
            existing.ImageUrl      = hotel.ImageUrl;
            existing.Description   = hotel.Description;
            existing.Amenities     = hotel.Amenities;

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeleteHotelAsync(Guid id, string managerId)
        {
            var hotel = await _hotelRepository.GetByIdAsync(id);
            if (hotel == null) return false;

            if (hotel.ManagerId != managerId) return false;

            /*
             * We attempt to delete the Cloudinary image before removing the database record. If the
             * Cloudinary call fails for any reason (network issue, invalid URL, etc.), the service
             * catches the exception, logs it, and continues with the database deletion. We made this
             * decision deliberately — a failed image cleanup should not prevent the hotel from being
             * deleted. The worst case is an orphaned image in Cloudinary, which is a minor storage
             * cost rather than a broken user experience.
             */
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
