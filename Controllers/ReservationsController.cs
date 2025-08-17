using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;

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
        public async Task<ActionResult<IEnumerable<Reservation>>> GetReservations()
        {
            var reservations = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Where(r => r.IsActive)
                .ToListAsync();

            // Debug: Log all reservations
            Console.WriteLine($"Total reservations found: {reservations.Count}");
            foreach (var r in reservations)
            {
                Console.WriteLine($"  ID: {r.Id}, Client: {r.ClientId}, Start: {r.StartDate:yyyy-MM-dd}, End: {r.EndDate:yyyy-MM-dd}, Status: {r.Status}, IsActive: {r.IsActive}");
            }

            return reservations;
        }

        // GET: api/Reservations/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Reservation>> GetReservation(int id)
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

            return reservation;
        }

        // GET: api/Reservations/client/5
        [HttpGet("client/{clientId}")]
        public async Task<ActionResult<IEnumerable<Reservation>>> GetReservationsByClient(int clientId)
        {
            return await _context.Reservations
                        .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                .Where(r => r.ClientId == clientId && r.IsActive)
                .ToListAsync();
        }

        // POST: api/Reservations
        [HttpPost]
        public async Task<ActionResult<Reservation>> PostReservation(Reservation reservation)
        {
            // Validate reservation dates
            if (reservation.StartDate >= reservation.EndDate)
            {
                return BadRequest("Start date must be before end date");
            }

            if (reservation.StartDate < DateTime.Today)
            {
                return BadRequest("Start date cannot be in the past");
            }

            // Calculate total price based on reservation items
            decimal totalPrice = 0;
            if (reservation.ReservationItems != null && reservation.ReservationItems.Any())
            {
                foreach (var item in reservation.ReservationItems)
                {
                    var article = await _context.Articles.FindAsync(item.ArticleId);
                    if (article == null || !article.IsActive)
                    {
                        return BadRequest($"Article with ID {item.ArticleId} not found");
                    }

                    // Check availability
                    var days = (reservation.EndDate - reservation.StartDate).Days;
                    var totalNeeded = item.Quantity * days;

                    // Check if enough quantity is available for the date range
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

            reservation.TotalPrice = totalPrice;
            reservation.Status = ReservationStatus.EnAttente;
            reservation.CreatedAt = DateTime.UtcNow;
            reservation.IsActive = true;

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetReservation), new { id = reservation.Id }, reservation);
        }

        // PUT: api/Reservations/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutReservation(int id, Reservation reservation)
        {
            if (id != reservation.Id)
            {
                return BadRequest();
            }

            var existingReservation = await _context.Reservations.FindAsync(id);
            if (existingReservation == null || !existingReservation.IsActive)
            {
                return NotFound();
            }

            // Only allow status updates for existing reservations
            existingReservation.Status = reservation.Status;
            existingReservation.IsActive = true;

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

            // Debug: Log the date range we're searching for
            Console.WriteLine($"Searching for reservations between {startDate:yyyy-MM-dd} and {endDate:yyyy-MM-dd}");

            var reservations = await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                .Where(r => r.IsActive &&
                           (r.StartDate.Date <= endDate.Date && r.EndDate.Date >= startDate.Date))
                .ToListAsync();

            // Debug: Log found reservations
            Console.WriteLine($"Found {reservations.Count} reservations:");
            foreach (var r in reservations)
            {
                Console.WriteLine($"  ID: {r.Id}, Client: {r.ClientId}, Start: {r.StartDate:yyyy-MM-dd HH:mm:ss}, End: {r.EndDate:yyyy-MM-dd HH:mm:ss}, Status: {r.Status}");
            }

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
                    revenue = Math.Round(dayRevenue, 2)
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

        // GET: api/Reservations/debug/test
        [HttpGet("debug/test")]
        public async Task<ActionResult<object>> DebugTest()
        {
            // Test 1: Get all reservations
            var allReservations = await _context.Reservations.ToListAsync();

            // Test 2: Get specific reservation by ID
            var reservation1 = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == 1);

            // Test 3: Get client
            var client51 = await _context.Clients.FirstOrDefaultAsync(c => c.Id == 51);

            // Test 4: Check August 2025 reservations
            var august2025Start = new DateTime(2025, 8, 1);
            var august2025End = new DateTime(2025, 8, 31);
            var augustReservations = await _context.Reservations
                .Where(r => r.IsActive &&
                           ((r.StartDate >= august2025Start && r.StartDate <= august2025End) ||
                            (r.EndDate >= august2025Start && r.EndDate <= august2025End) ||
                            (r.StartDate <= august2025Start && r.EndDate >= august2025End)))
                .ToListAsync();

            return Ok(new
            {
                totalReservations = allReservations.Count,
                allReservations = allReservations.Select(r => new
                {
                    id = r.Id,
                    clientId = r.ClientId,
                    startDate = r.StartDate.ToString("yyyy-MM-dd"),
                    endDate = r.EndDate.ToString("yyyy-MM-dd"),
                    status = r.Status.ToString(),
                    isActive = r.IsActive
                }),
                reservation1 = reservation1 != null ? new
                {
                    id = reservation1.Id,
                    clientId = reservation1.ClientId,
                    startDate = reservation1.StartDate.ToString("yyyy-MM-dd"),
                    endDate = reservation1.EndDate.ToString("yyyy-MM-dd"),
                    status = reservation1.Status.ToString(),
                    isActive = reservation1.IsActive
                } : null,
                client51 = client51 != null ? new
                {
                    id = client51.Id,
                    name = client51.Name
                } : null,
                augustReservations = augustReservations.Select(r => new
                {
                    id = r.Id,
                    clientId = r.ClientId,
                    startDate = r.StartDate.ToString("yyyy-MM-dd"),
                    endDate = r.EndDate.ToString("yyyy-MM-dd"),
                    status = r.Status.ToString(),
                    isActive = r.IsActive
                })
            });
        }
    }
}
