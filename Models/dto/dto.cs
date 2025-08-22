using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LocationDeco.API.Models;

namespace LocationDeco.API.DTOs
{
    public class ArticleAvailabilityDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? PricePerDay { get; set; }
        public int QuantityTotal { get; set; }
        public int QuantityAvailable { get; set; }
        public CategoryDto? Category { get; set; }
    }

    public class ReservationCreateDto
    {
        public int Id { get; set; }

        public int ClientId { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.EnAttente;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPrice { get; set; }
        
        public string? Remarques { get; set; }
        
        public virtual ICollection<ReservationItemCreateDto> ReservationItems { get; set; } = new List<ReservationItemCreateDto>();

    }
    public class ReservationItemCreateDto
    {
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
    }
    public class ReservationDto
    {
        public int Id { get; set; }
        public int ClientId { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public ReservationStatus Status { get; set; }
        public decimal TotalPrice { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; }
        public string? Notes { get; set; }

        // Navigation properties
        public ClientDto? Client { get; set; }
        public List<ReservationItemDto> ReservationItems { get; set; } = new();
        public List<PaymentDto> Payments { get; set; } = new();
    }

    public class ClientDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
    }

    public class ReservationItemDto
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public int ArticleId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public int? DurationDays { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public ArticleDto? Article { get; set; }
    }

    public class ArticleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal? PricePerDay { get; set; }
        public int QuantityTotal { get; set; }
        public CategoryDto? Category { get; set; }
    }

    public class CategoryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class PaymentDto
    {
        public int Id { get; set; }
        public int ReservationId { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string Method { get; set; } = string.Empty;
        public string? Note { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}