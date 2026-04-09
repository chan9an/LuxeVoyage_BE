using System.ComponentModel.DataAnnotations;
using Hotel.API.Enums;

namespace Hotel.API.Entities
{
    public class RoomType
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public Guid HotelId { get; set; }
        public HotelEntity? Hotel { get; set; }

        public string Name { get; set; } = string.Empty;
        
        public RoomCategory Category { get; set; }
        
        public decimal PricePerNight { get; set; }
        public int MaxOccupancy { get; set; }
    }
}
