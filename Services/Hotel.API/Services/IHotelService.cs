using Hotel.API.Repositories;

using Hotel.API.Entities;

namespace Hotel.API.Services
{
    public interface IHotelService
    {
        Task<IEnumerable<HotelEntity>> GetAllHotelsAsync();
        Task<HotelEntity?> GetHotelByIdAsync(Guid id);
        Task<HotelEntity> CreateHotelAsync(HotelEntity hotel);
        Task UpdateHotelAsync(HotelEntity hotel);
        Task DeleteHotelAsync(Guid id);
        Task<IEnumerable<HotelEntity>> GetHotelsByCityAsync(string city);
    }
}
