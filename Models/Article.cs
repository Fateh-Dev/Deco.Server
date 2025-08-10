using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    
    public class Article
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public int CategoryId { get; set; }
        
        [StringLength(500)]
        public string? Description { get; set; }
        
        public int QuantityTotal { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PricePerDay { get; set; }
        
        [StringLength(500)]
        public string? ImageUrl { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        // [JsonIgnore]
        public virtual Category? Category { get; set; } = null!;
        [JsonIgnore]
        public virtual ICollection<ReservationItem> ReservationItems { get; set; } = new List<ReservationItem>();
    }
}
