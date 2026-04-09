using System.ComponentModel.DataAnnotations;

namespace Hotel.API.Entities
{
    public class Room
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string RoomNumber { get; set; } = string.Empty;
        public bool IsAvailable { get; set; } = true;

        public Guid RoomTypeId { get; set; }
        public RoomType? RoomType { get; set; }

        public Guid HotelId { get; set; }
        public HotelEntity? Hotel { get; set; }
    }
}
