using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Hotel.API.Domain.Entities
{
    public class RoomType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int PropertyId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseNightlyRateUSD { get; set; }

        // Navigation property for Property
        [ForeignKey(nameof(PropertyId))]
        public Property? Property { get; set; }
    }
}
