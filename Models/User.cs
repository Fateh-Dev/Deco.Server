using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    public class User
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        [Required]
        [StringLength(20)]
        public string Phone { get; set; } = string.Empty;
        
        [EmailAddress]
        [StringLength(100)]
        public string? Email { get; set; }
        
        [StringLength(50)]
        public string? EventType { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
