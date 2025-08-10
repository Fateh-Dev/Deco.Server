using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace LocationDeco.API.Models
{
    public class Category
    {
        public int Id { get; set; }
        
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public bool IsActive { get; set; } = true;
        
        // Navigation properties
        [JsonIgnore]
        public virtual ICollection<Article> Articles { get; set; } = new List<Article>();
    }
}
