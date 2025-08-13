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
            return await _context.Reservations
                .Include(r => r.Client)
                .Include(r => r.ReservationItems)
                    .ThenInclude(ri => ri.Article)
                        .ThenInclude(a => a.Category)
                .Where(r => r.IsActive)
                .ToListAsync();
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

        private bool ReservationExists(int id)
        {
            return _context.Reservations.Any(e => e.Id == id && e.IsActive);
        }
    }
}
