using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    public enum ReservationStatus
    {
        EnAttente,
        Confirmee,
        Annulee,
        Terminee
    }

    public class Reservation
    {
        public int Id { get; set; }
        
        public int UserId { get; set; }
        
        [Required]
        public DateTime StartDate { get; set; }
        
        [Required]
        public DateTime EndDate { get; set; }
        
        public ReservationStatus Status { get; set; } = ReservationStatus.EnAttente;
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        [JsonIgnore]
        public virtual User User { get; set; } = null!;
        [JsonIgnore]
        public virtual ICollection<ReservationItem> ReservationItems { get; set; } = new List<ReservationItem>();
        [JsonIgnore]
        public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    }
}
