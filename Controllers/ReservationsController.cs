using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;
using LocationDeco.API.DTOs;

namespace LocationDeco.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReservationsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Reservations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservations()
        {
            var reservations = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Include(r => r.Payments)
                .Where(r => r.IsActive)
                .ToListAsync();

            var reservationDtos = reservations.Select(r => MapToDto(r)).ToList();
            return Ok(reservationDtos);
        }

        // GET: api/Reservations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ReservationDto>> GetReservation(int id)
        {
            var reservation = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (reservation == null)
            {
                return NotFound();
            }

            var reservationDto = MapToDto(reservation);
            return Ok(reservationDto);
        }

        // GET: api/Reservations/client/5
        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservationsByClient(int clientId)
        {
            var reservations = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Include(r => r.Payments)
                .Where(r => r.ClientId == clientId && r.IsActive)
                .ToListAsync();

            var reservationDtos = reservations.Select(r => MapToDto(r)).ToList();
            return Ok(reservationDtos);
        }

        // POST: api/Reservations
        [HttpPost]
        public async Task<ActionResult<ReservationDto>> PostReservation(ReservationCreateDto reservation)
        {

            // Validate reservation dates
            if (reservation.StartDate > reservation.EndDate)
            {
                return BadRequest("Start date must be before end date");
            }

            // Removed past date validation to allow reservations with past dates

            // Calculate total price based on reservation items
            decimal totalPrice = 0;
            if (reservation.ReservationItems != null && reservation.ReservationItems.Any())
            {
                var days = (reservation.EndDate - reservation.StartDate).Days + 1;
                if (days == 0) days = 1; // Minimum 1 day

                foreach (var item in reservation.ReservationItems)
                {
                    var article = await _context.Articles.FindAsync(item.ArticleId);
                    if (article == null || !article.IsActive)
                    {
                        return BadRequest($"Article with ID {item.ArticleId} not found");
                    }

                    // Check availability
                    var reservedQuantity = await _context.ReservationItems
                        .Where(ri => ri.ArticleId == item.ArticleId)
                        .Where(ri => ri.Reservation.StartDate <= reservation.EndDate &&
                                   ri.Reservation.EndDate >= reservation.StartDate &&
                                   ri.Reservation.Status != ReservationStatus.Annulee &&
                                   ri.Reservation.IsActive)
                        .SumAsync(ri => ri.Quantity);

                    var availableQuantity = article.QuantityTotal - reservedQuantity;
                    if (availableQuantity < item.Quantity)
                    {
                        return BadRequest($"Not enough quantity available for article {article.Name}. Available: {availableQuantity}, Requested: {item.Quantity}");
                    }

                    item.UnitPrice = article.PricePerDay ?? 0;
                    totalPrice += item.UnitPrice * item.Quantity * days;
                }
            }

            var reservationEntity = new Reservation();
            reservationEntity.ClientId = reservation.ClientId;
            reservationEntity.StartDate = reservation.StartDate;
            reservationEntity.EndDate = reservation.EndDate;
            reservationEntity.TotalPrice = totalPrice;
            reservationEntity.Status = ReservationStatus.EnAttente;
            reservationEntity.CreatedAt = DateTime.UtcNow;
            reservationEntity.IsActive = true;
            reservationEntity.Remarques = reservation.Remarques;

            _context.Reservations.Add(reservationEntity);
            await _context.SaveChangesAsync();

            // Create ReservationItems in the database
            if (reservation.ReservationItems != null && reservation.ReservationItems.Any())
            {
                foreach (var item in reservation.ReservationItems)
                {
                    var reservationItemEntity = new ReservationItem
                    {
                        ReservationId = reservationEntity.Id,
                        ArticleId = item.ArticleId,
                        Quantity = item.Quantity,
                        UnitPrice = item.UnitPrice,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.ReservationItems.Add(reservationItemEntity);
                }
                await _context.SaveChangesAsync();
            }

            // Load the complete reservation with all related data
            var createdReservation = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Include(r => r.Payments)
                .FirstOrDefaultAsync(r => r.Id == reservationEntity.Id);

            var reservationDto = MapToDto(createdReservation!);
            return CreatedAtAction(nameof(GetReservation), new { id = reservationEntity.Id }, reservationDto);
        }

       // PUT: api/Reservations/5
[HttpPut("{id}")]
public async Task<IActionResult> PutReservation(int id, ReservationCreateDto reservation)
{
    // Find existing reservation with related data
    var existingReservation = await _context.Reservations
        .Include(r => r.ReservationItems)
        .FirstOrDefaultAsync(r => r.Id == id);

    if (existingReservation == null || !existingReservation.IsActive)
    {
        return NotFound();
    }

    // Validate reservation dates
    if (reservation.StartDate > reservation.EndDate)
    {
        return BadRequest("Start date must be before end date");
    }

    // Calculate total price based on reservation items
    decimal totalPrice = 0;
    if (reservation.ReservationItems != null && reservation.ReservationItems.Any())
    {
        var days = (reservation.EndDate - reservation.StartDate).Days + 1;
        if (days == 0) days = 1; // Minimum 1 day

        foreach (var item in reservation.ReservationItems)
        {
            var article = await _context.Articles.FindAsync(item.ArticleId);
            if (article == null || !article.IsActive)
            {
                return BadRequest($"Article with ID {item.ArticleId} not found");
            }

            // Check availability (excluding current reservation items)
            var reservedQuantity = await _context.ReservationItems
                .Where(ri => ri.ArticleId == item.ArticleId)
                .Where(ri => ri.ReservationId != id) // Exclude current reservation
                .Where(ri => ri.Reservation.StartDate <= reservation.EndDate &&
                           ri.Reservation.EndDate >= reservation.StartDate &&
                           ri.Reservation.Status != ReservationStatus.Annulee &&
                           ri.Reservation.IsActive)
                .SumAsync(ri => ri.Quantity);

            var availableQuantity = article.QuantityTotal - reservedQuantity;
            if (availableQuantity < item.Quantity)
            {
                return BadRequest($"Not enough quantity available for article {article.Name}. Available: {availableQuantity}, Requested: {item.Quantity}");
            }

            item.UnitPrice = article.PricePerDay ?? 0;
            totalPrice += item.UnitPrice * item.Quantity * days;
        }
    }

    // Update reservation properties
    existingReservation.ClientId = reservation.ClientId;
    existingReservation.StartDate = reservation.StartDate;
    existingReservation.EndDate = reservation.EndDate;
    existingReservation.TotalPrice = totalPrice;
    existingReservation.Remarques = reservation.Remarques;
    // Note: Keep existing Status, CreatedAt, and IsActive unchanged
    // existingReservation.UpdatedAt = DateTime.UtcNow; // Add this if you have UpdatedAt field

    // Remove existing reservation items
    if (existingReservation.ReservationItems != null && existingReservation.ReservationItems.Any())
    {
        _context.ReservationItems.RemoveRange(existingReservation.ReservationItems);
    }

    // Create new ReservationItems
    if (reservation.ReservationItems != null && reservation.ReservationItems.Any())
    {
        foreach (var item in reservation.ReservationItems)
        {
            var reservationItemEntity = new ReservationItem
            {
                ReservationId = existingReservation.Id,
                ArticleId = item.ArticleId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                CreatedAt = DateTime.UtcNow
            };
            _context.ReservationItems.Add(reservationItemEntity);
        }
    }

    try
    {
        await _context.SaveChangesAsync();
    }
    catch (DbUpdateConcurrencyException)
    {
        if (!ReservationExists(id))
        {
            return NotFound();
        }
        else
        {
            throw;
        }
    }

    return NoContent();
}

   // DELETE: api/Reservations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null || !reservation.IsActive)
            {
                return NotFound();
            }

            reservation.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Reservations/calendar/{year}/{month}
        [HttpGet("calendar/{year}/{month}")]
        public async Task<ActionResult<object>> GetCalendarData(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);

            var reservations = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Where(r => r.IsActive &&
                           (r.StartDate.Date <= endDate.Date && r.EndDate.Date >= startDate.Date))
                .ToListAsync();

            var articlesWithSceneCategory = await _context.Articles
                .Include(a => a.Category)
                .Where(a => a.Category != null && a.Category.Name.Contains("scene"))
                .ToListAsync();

            var calendarData = new List<object>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var dayReservations = reservations.Where(r =>
                    currentDate.Date >= r.StartDate.Date && currentDate.Date <= r.EndDate.Date).ToList();

                var dayRevenue = dayReservations
                    .Where(r => r.Status != ReservationStatus.Annulee)
                    .Sum(r =>
                    {
                        var days = Math.Max(1, (r.EndDate.Date - r.StartDate.Date).Days + 1);
                        return (decimal)r.TotalPrice / days;
                    });

                calendarData.Add(new
                {
                    date = currentDate.ToString("yyyy-MM-dd"),
                    day = currentDate.Day,
                    isCurrentMonth = true,
                    isToday = currentDate.Date == DateTime.Today,
                    isWeekend = currentDate.DayOfWeek == DayOfWeek.Saturday || currentDate.DayOfWeek == DayOfWeek.Sunday,
                    reservations = dayReservations.Select(r => new
                    {
                        id = r.Id,
                        clientId = r.ClientId,
                        clientName = r.Client?.Name,
                        startDate = r.StartDate.ToString("yyyy-MM-dd"),
                        endDate = r.EndDate.ToString("yyyy-MM-dd"),
                        totalPrice = r.TotalPrice,
                        status = r.Status.ToString(),
                        statusLabel = GetStatusLabel(r.Status)
                    }).ToList(),
                    hasReservations = dayReservations.Any(),
                    revenue = Math.Round(dayRevenue, 2),
                    articlesWithSceneCategory = articlesWithSceneCategory.Select(a => new
                    {
                        id = a.Id,
                        name = a.Name,
                        description = a.Description,
                        pricePerDay = a.PricePerDay,
                        categoryName = a.Category?.Name
                    }).ToList()
                });

                currentDate = currentDate.AddDays(1);
            }

            return Ok(new
            {
                year = year,
                month = month,
                monthName = GetMonthName(month),
                days = calendarData
            });
        }
        private ReservationDto MapToDto_(Reservation reservation)
        {
            // Ignore the time component and only consider the date part for the calculation.
            var startDateOnly = reservation.StartDate.Date;
            var endDateOnly = reservation.EndDate.Date;

            // Calculate the duration in days.
            var durationDays = (endDateOnly - startDateOnly).Days + 1;

            // Calculate the total price based on all reservation items.
            var calculatedTotalPrice = reservation.ReservationItems?
                .Sum(ri => (ri.Quantity * ri.UnitPrice * durationDays)) ?? 0;

            var dto = new ReservationDto
            {
                Id = reservation.Id,
                ClientId = reservation.ClientId,
                StartDate = reservation.StartDate,
                EndDate = reservation.EndDate,
                Status = reservation.Status,
                // Use the dynamically calculated total price.
                TotalPrice = calculatedTotalPrice,
                CreatedAt = reservation.CreatedAt,
                IsActive = reservation.IsActive,
                Client = reservation.Client != null ? new ClientDto
                {
                    Id = reservation.Client.Id,
                    Name = reservation.Client.Name,
                    Phone = reservation.Client.Phone,
                    Email = reservation.Client.Email,
                    Address = reservation.Client.Address
                } : null,
                ReservationItems = reservation.ReservationItems?.Select(ri => new ReservationItemDto
                {
                    Id = ri.Id,
                    ReservationId = ri.ReservationId,
                    ArticleId = ri.ArticleId,
                    Quantity = ri.Quantity,
                    UnitPrice = ri.UnitPrice,
                    // Use the consistent, date-only duration.
                    DurationDays = durationDays,
                    CreatedAt = ri.CreatedAt,
                    Article = ri.Article != null ? new ArticleDto
                    {
                        Id = ri.Article.Id,
                        Name = ri.Article.Name,
                        Description = ri.Article.Description,
                        PricePerDay = ri.Article.PricePerDay,
                        QuantityTotal = ri.Article.QuantityTotal,
                        Category = ri.Article.Category != null ? new CategoryDto
                        {
                            Id = ri.Article.Category.Id,
                            Name = ri.Article.Category.Name
                        } : null
                    } : null
                }).ToList() ?? new List<ReservationItemDto>(),
                Payments = reservation.Payments?.Select(p => new PaymentDto
                {
                    Id = p.Id,
                    ReservationId = p.ReservationId,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    Method = p.Method,
                    Note = p.Note,
                    CreatedAt = p.CreatedAt
                }).ToList() ?? new List<PaymentDto>()
            };

            return dto;
        }
        private ReservationDto MapToDto(Reservation reservation)
        {
            // Ignore the time component and only consider the date part for the calculation.
            var startDateOnly = reservation.StartDate.Date;
            var endDateOnly = reservation.EndDate.Date;

            // Calculate the duration in days.
            var durationDays = (endDateOnly - startDateOnly).Days + 1;

            var dto = new ReservationDto
            {
                Id = reservation.Id,
                ClientId = reservation.ClientId,
                StartDate = reservation.StartDate,
                EndDate = reservation.EndDate,
                Status = reservation.Status,
                TotalPrice = reservation.TotalPrice,
                CreatedAt = reservation.CreatedAt,
                IsActive = reservation.IsActive,
                Notes = reservation.Remarques,
                Client = reservation.Client != null ? new ClientDto
                {
                    Id = reservation.Client.Id,
                    Name = reservation.Client.Name,
                    Phone = reservation.Client.Phone,
                    Email = reservation.Client.Email,
                    Address = reservation.Client.Address
                } : null,
                ReservationItems = reservation.ReservationItems?.Select(ri => new ReservationItemDto
                {
                    Id = ri.Id,
                    ReservationId = ri.ReservationId,
                    ArticleId = ri.ArticleId,
                    Quantity = ri.Quantity,
                    UnitPrice = ri.UnitPrice,
                    // Use the consistent, date-only duration.
                    DurationDays = durationDays,
                    CreatedAt = ri.CreatedAt,
                    Article = ri.Article != null ? new ArticleDto
                    {
                        Id = ri.Article.Id,
                        Name = ri.Article.Name,
                        Description = ri.Article.Description,
                        PricePerDay = ri.Article.PricePerDay,
                        QuantityTotal = ri.Article.QuantityTotal,
                        Category = ri.Article.Category != null ? new CategoryDto
                        {
                            Id = ri.Article.Category.Id,
                            Name = ri.Article.Category.Name
                        } : null
                    } : null
                }).ToList() ?? new List<ReservationItemDto>(),
                Payments = reservation.Payments?.Select(p => new PaymentDto
                {
                    Id = p.Id,
                    ReservationId = p.ReservationId,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    Method = p.Method,
                    Note = p.Note,
                    CreatedAt = p.CreatedAt
                }).ToList() ?? new List<PaymentDto>()
            };

            return dto;
        }

        private string GetStatusLabel(ReservationStatus status)
        {
            return status switch
            {
                ReservationStatus.EnAttente => "En attente",
                ReservationStatus.Confirmee => "Confirmée",
                ReservationStatus.Annulee => "Annulée",
                ReservationStatus.Terminee => "Terminée",
                _ => status.ToString()
            };
        }

        private string GetMonthName(int month)
        {
            return month switch
            {
                1 => "Janvier",
                2 => "Février",
                3 => "Mars",
                4 => "Avril",
                5 => "Mai",
                6 => "Juin",
                7 => "Juillet",
                8 => "Août",
                9 => "Septembre",
                10 => "Octobre",
                11 => "Novembre",
                12 => "Décembre",
                _ => "Mois inconnu"
            };
        }

        private bool ReservationExists(int id)
        {
            return _context.Reservations.Any(e => e.Id == id && e.IsActive);
        }
    }
}