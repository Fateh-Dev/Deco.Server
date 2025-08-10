using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    public class Payment
    {
        public int Id { get; set; }
        
        public int ReservationId { get; set; }
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }
        
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
        
        [StringLength(50)]
        public string Method { get; set; } = string.Empty;
        
        [StringLength(500)]
        public string? Note { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        [JsonIgnore]
        public virtual Reservation Reservation { get; set; } = null!;
    }
}
