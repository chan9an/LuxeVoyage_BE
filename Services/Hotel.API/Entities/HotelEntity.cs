using Hotel.API.Enums;

namespace Hotel.API.Entities
{
    public class HotelEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        
        public decimal PricePerNight { get; set; }
        public string Currency { get; set; } = "INR";
        
        public string ImageUrl { get; set; } = string.Empty;
        
        public PropertyType Type { get; set; }
        
        public decimal Rating { get; set; } 
        public int ReviewCount { get; set; } 
        
        // EF Core 8 maps List of Enums to JSON arrays out of the box (primitive collections)!
        public List<Amenity> Amenities { get; set; } = new();

        public virtual ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();
        public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();
    }
}
