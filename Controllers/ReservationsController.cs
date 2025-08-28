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
        private static readonly Dictionary<int, string> MonthNames = new()
        {
            { 1, "Janvier" }, { 2, "Février" }, { 3, "Mars" }, { 4, "Avril" },
            { 5, "Mai" }, { 6, "Juin" }, { 7, "Juillet" }, { 8, "Août" },
            { 9, "Septembre" }, { 10, "Octobre" }, { 11, "Novembre" }, { 12, "Décembre" }
        };

        private static readonly Dictionary<ReservationStatus, string> StatusLabels = new()
        {
            { ReservationStatus.EnAttente, "En attente" },
            { ReservationStatus.Confirmee, "Confirmée" },
            { ReservationStatus.Annulee, "Annulée" },
            { ReservationStatus.Terminee, "Terminée" }
        };

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Reservations
        [HttpGet]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservations()
        {
            var reservations = await GetReservationsQuery()
                .Where(r => r.IsActive)
                .ToListAsync();

            return Ok(reservations.Select(MapToDto));
        }

        // GET: api/Reservations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<ReservationDto>> GetReservation(int id)
        {
            var reservation = await GetReservationsQuery()
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (reservation == null)
                return NotFound();

            return Ok(MapToDto(reservation));
        }

        // GET: api/Reservations/client/5
        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<ReservationDto>>> GetReservationsByClient(int clientId)
        {
            var reservations = await GetReservationsQuery()
                .Where(r => r.ClientId == clientId && r.IsActive)
                .ToListAsync();

            return Ok(reservations.Select(MapToDto));
        }

        // POST: api/Reservations
        [HttpPost]
        public async Task<ActionResult<ReservationDto>> PostReservation(ReservationCreateDto reservationDto)
        {
            // Validate dates
            var validationResult = ValidateReservationDates(reservationDto.StartDate, reservationDto.EndDate);
            if (validationResult != null)
                return validationResult;

            // Calculate total and validate items
            var (totalPrice, validationError) = await CalculateTotalPriceAndValidateItems(
                reservationDto.ReservationItems, 
                reservationDto.StartDate, 
                reservationDto.EndDate);
                
            if (validationError != null)
                return validationError;

            // Create reservation entity
            var reservation = new Reservation
            {
                ClientId = reservationDto.ClientId,
                StartDate = reservationDto.StartDate,
                EndDate = reservationDto.EndDate,
                TotalPrice = totalPrice,
                Status = ReservationStatus.EnAttente,
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                Remarques = reservationDto.Remarques
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // Add reservation items
            await AddReservationItems(reservation.Id, reservationDto.ReservationItems);

            // Return created reservation
            var createdReservation = await GetReservationsQuery()
                .FirstAsync(r => r.Id == reservation.Id);

            var result = MapToDto(createdReservation);
            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, result);
        }

        // PUT: api/Reservations/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReservation(int id, ReservationCreateDto reservationDto)
        {
            var existingReservation = await _context.Reservations
                .Include(r => r.ReservationItems)
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (existingReservation == null)
                return NotFound();

            // Validate dates
            var validationResult = ValidateReservationDates(reservationDto.StartDate, reservationDto.EndDate);
            if (validationResult != null)
                return validationResult;

            // Calculate total and validate items (excluding current reservation)
            var (totalPrice, validationError) = await CalculateTotalPriceAndValidateItems(
                reservationDto.ReservationItems,
                reservationDto.StartDate,
                reservationDto.EndDate,
                id);

            if (validationError != null)
                return validationError;

            // Update reservation
            existingReservation.ClientId = reservationDto.ClientId;
            existingReservation.StartDate = reservationDto.StartDate;
            existingReservation.EndDate = reservationDto.EndDate;
            existingReservation.TotalPrice = totalPrice;
            existingReservation.Remarques = reservationDto.Remarques;

            // Replace reservation items
            await ReplaceReservationItems(id, reservationDto.ReservationItems);

            try
            {
                await _context.SaveChangesAsync();
                return NoContent();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await ReservationExistsAsync(id))
                    return NotFound();
                throw;
            }
        }

        // DELETE: api/Reservations/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReservation(int id)
        {
            var reservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);

            if (reservation == null)
                return NotFound();

            reservation.IsActive = false;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Reservations/articles/availability
        [HttpGet("articles/availability")]
        public async Task<ActionResult<IEnumerable<ArticleAvailabilityDto>>> GetArticlesAvailability(
            [FromQuery] DateTime startDate, 
            [FromQuery] DateTime endDate)
        {
            if (startDate > endDate)
                return BadRequest("Start date must be before end date");

            // Get articles with pre-calculated availability
            var availabilityData = await GetArticleAvailabilityData(startDate, endDate);
            
            return Ok(availabilityData);
        }

        // GET: api/Reservations/calendar/{year}/{month}
        [HttpGet("calendar/{year}/{month}")]
        public async Task<ActionResult<object>> GetCalendarData(int year, int month)
        {
            var (startDate, endDate) = GetMonthRange(year, month);

            var reservations = await GetReservationsQuery()
                .Where(r => r.IsActive && r.StartDate.Date <= endDate.Date && r.EndDate.Date >= startDate.Date)
                .ToListAsync();

            var sceneArticles = await GetSceneArticles();
            var calendarData = BuildCalendarData(startDate, endDate, reservations, sceneArticles);

            return Ok(new
            {
                year,
                month,
                monthName = MonthNames.GetValueOrDefault(month, "Mois inconnu"),
                days = calendarData
            });
        }

        #region Private Helper Methods

        private IQueryable<Reservation> GetReservationsQuery()
        {
            return _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Include(r => r.Payments);
        }

        private BadRequestObjectResult? ValidateReservationDates(DateTime startDate, DateTime endDate)
        {
            return startDate > endDate ? BadRequest("Start date must be before end date") : null;
        }

        private async Task<(decimal totalPrice, BadRequestObjectResult? error)> CalculateTotalPriceAndValidateItems(
            ICollection<ReservationItemCreateDto> items,
            DateTime startDate,
            DateTime endDate,
            int? excludeReservationId = null)
        {
            if (items == null || !items.Any())
                return (0, null);

            var days = Math.Max(1, (endDate.Date - startDate.Date).Days + 1);
            decimal totalPrice = 0;

            // Get all article IDs at once
            var articleIds = items.Select(i => i.ArticleId).ToList();
            var articles = await _context.Articles
                .Where(a => articleIds.Contains(a.Id) && a.IsActive)
                .ToDictionaryAsync(a => a.Id);

            // Get reserved quantities for all articles at once
            var reservedQuantities = await GetReservedQuantities(articleIds, startDate, endDate, excludeReservationId);

            foreach (var item in items)
            {
                // Validate article exists
                if (!articles.TryGetValue(item.ArticleId, out var article))
                    return (0, BadRequest($"Article with ID {item.ArticleId} not found"));

                // Check availability
                var reservedQuantity = reservedQuantities.GetValueOrDefault(item.ArticleId, 0);
                var availableQuantity = article.QuantityTotal - reservedQuantity;
                
                if (availableQuantity < item.Quantity)
                    return (0, BadRequest($"Not enough quantity available for article {article.Name}. Available: {availableQuantity}, Requested: {item.Quantity}"));

                // Calculate price
                item.UnitPrice = article.PricePerDay ?? 0;
                totalPrice += item.UnitPrice * item.Quantity * days;
            }

            return (totalPrice, null);
        }

        private async Task<Dictionary<int, int>> GetReservedQuantities(
            List<int> articleIds, 
            DateTime startDate, 
            DateTime endDate, 
            int? excludeReservationId = null)
        {
            var query = _context.ReservationItems
                .Where(ri => articleIds.Contains(ri.ArticleId))
                .Where(ri => ri.Reservation.StartDate <= endDate &&
                           ri.Reservation.EndDate >= startDate &&
                           ri.Reservation.Status != ReservationStatus.Annulee &&
                           ri.Reservation.IsActive);

            if (excludeReservationId.HasValue)
                query = query.Where(ri => ri.ReservationId != excludeReservationId.Value);

            return await query
                .GroupBy(ri => ri.ArticleId)
                .ToDictionaryAsync(g => g.Key, g => g.Sum(ri => ri.Quantity));
        }

        private async Task AddReservationItems(int reservationId, ICollection<ReservationItemCreateDto> items)
        {
            if (items?.Any() != true) return;

            var reservationItems = items.Select(item => new ReservationItem
            {
                ReservationId = reservationId,
                ArticleId = item.ArticleId,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.ReservationItems.AddRange(reservationItems);
            await _context.SaveChangesAsync();
        }

        private async Task ReplaceReservationItems(int reservationId, ICollection<ReservationItemCreateDto> newItems)
        {
            // Remove existing items
            var existingItems = await _context.ReservationItems
                .Where(ri => ri.ReservationId == reservationId)
                .ToListAsync();

            if (existingItems.Any())
                _context.ReservationItems.RemoveRange(existingItems);

            // Add new items
            await AddReservationItems(reservationId, newItems);
        }

        private async Task<List<ArticleAvailabilityDto>> GetArticleAvailabilityData(DateTime startDate, DateTime endDate)
        {
            var articles = await _context.Articles
                .Include(a => a.Category)
                .Where(a => a.IsActive)
                .ToListAsync();

            var articleIds = articles.Select(a => a.Id).ToList();
            var reservedQuantities = await GetReservedQuantities(articleIds, startDate, endDate);

            return articles.Select(article => new ArticleAvailabilityDto
            {
                Id = article.Id,
                Name = article.Name,
                Description = article.Description,
                PricePerDay = article.PricePerDay,
                QuantityTotal = article.QuantityTotal,
                QuantityAvailable = Math.Max(0, article.QuantityTotal - reservedQuantities.GetValueOrDefault(article.Id, 0)),
                Category = article.Category != null ? new CategoryDto
                {
                    Id = article.Category.Id,
                    Name = article.Category.Name
                } : null
            }).ToList();
        }

        private async Task<List<object>> GetSceneArticles()
        {
            return await _context.Articles
                .Include(a => a.Category)
                .Where(a => a.Category != null && a.Category.Name.Contains("scene"))
                .Select(a => new
                {
                    id = a.Id,
                    name = a.Name,
                    description = a.Description,
                    pricePerDay = a.PricePerDay,
                    categoryName = a.Category!.Name
                })
                .Cast<object>()
                .ToListAsync();
        }

        private static (DateTime startDate, DateTime endDate) GetMonthRange(int year, int month)
        {
            var startDate = new DateTime(year, month, 1);
            var endDate = startDate.AddMonths(1).AddDays(-1);
            return (startDate, endDate);
        }

        private List<object> BuildCalendarData(DateTime startDate, DateTime endDate, List<Reservation> reservations, List<object> sceneArticles)
        {
            var calendarData = new List<object>();
            var currentDate = startDate;

            while (currentDate <= endDate)
            {
                var dayReservations = reservations.Where(r =>
                    currentDate.Date >= r.StartDate.Date && currentDate.Date <= r.EndDate.Date).ToList();

                var dayRevenue = CalculateDayRevenue(dayReservations);

                calendarData.Add(new
                {
                    date = currentDate.ToString("yyyy-MM-dd"),
                    day = currentDate.Day,
                    isCurrentMonth = true,
                    isToday = currentDate.Date == DateTime.Today,
                    isWeekend = currentDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
                    reservations = dayReservations.Select(r => new
                    {
                        id = r.Id,
                        clientId = r.ClientId,
                        clientName = r.Client?.Name,
                        startDate = r.StartDate.ToString("yyyy-MM-dd"),
                        endDate = r.EndDate.ToString("yyyy-MM-dd"),
                        totalPrice = r.TotalPrice,
                        status = r.Status.ToString(),
                        statusLabel = StatusLabels.GetValueOrDefault(r.Status, r.Status.ToString())
                    }).ToList(),
                    hasReservations = dayReservations.Any(),
                    revenue = Math.Round(dayRevenue, 2),
                    articlesWithSceneCategory = sceneArticles
                });

                currentDate = currentDate.AddDays(1);
            }

            return calendarData;
        }

        private static decimal CalculateDayRevenue(List<Reservation> dayReservations)
        {
            return dayReservations
                .Where(r => r.Status != ReservationStatus.Annulee)
                .Sum(r =>
                {
                    var days = Math.Max(1, (r.EndDate.Date - r.StartDate.Date).Days + 1);
                    return r.TotalPrice / days;
                });
        }

        private ReservationDto MapToDto(Reservation reservation)
        {
            var durationDays = Math.Max(1, (reservation.EndDate.Date - reservation.StartDate.Date).Days + 1);

            return new ReservationDto
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
        }

        private async Task<bool> ReservationExistsAsync(int id)
        {
            return await _context.Reservations.AnyAsync(e => e.Id == id && e.IsActive);
        }

        #endregion
    }
}