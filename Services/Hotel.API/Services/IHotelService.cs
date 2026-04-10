using Hotel.API.Entities;

namespace Hotel.API.Services
{
    public interface IHotelService
    {
        Task<IEnumerable<HotelEntity>> GetAllHotelsAsync();
        Task<HotelEntity?> GetHotelByIdAsync(Guid id);
        Task<IEnumerable<HotelEntity>> GetHotelsByManagerAsync(string managerId);
        Task<HotelEntity> CreateHotelAsync(HotelEntity hotel, string managerId);
        Task<bool> UpdateHotelAsync(HotelEntity hotel, string managerId);
        Task<bool> DeleteHotelAsync(Guid id, string managerId);
        Task<IEnumerable<HotelEntity>> GetHotelsByCityAsync(string city);
    }
}
