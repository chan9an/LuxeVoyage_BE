using System.ComponentModel.DataAnnotations;

namespace Hotel.API.Domain.Entities
{
    public class Property
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Range(1, 5)]
        public int StarRating { get; set; }

        [Required]
        [MaxLength(100)]
        public string City { get; set; } = string.Empty;

        public bool HasPool { get; set; }

        public bool IsBeachfront { get; set; }

        // Navigation property for RoomTypes
        public ICollection<RoomType> RoomTypes { get; set; } = new List<RoomType>();
    }
}
