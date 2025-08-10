using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    public class ReservationItem
    {
        public int Id { get; set; }
        
        public int ReservationId { get; set; }
        
        public int ArticleId { get; set; }
        
        public int Quantity { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [JsonIgnore]
        public virtual Reservation Reservation { get; set; } = null!;
        [JsonIgnore]
        public virtual Article Article { get; set; } = null!;
    }
}
