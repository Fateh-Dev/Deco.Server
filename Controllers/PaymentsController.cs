using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;

namespace LocationDeco.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Payments
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Payment>>> GetPayments()
        {
            return await _context.Payments
                .Include(p => p.Reservation)
                .ToListAsync();
        }

        // GET: api/Payments/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Payment>> GetPayment(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Reservation)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                return NotFound();
            }

            return payment;
        }

        // GET: api/Payments/reservation/5
        [HttpGet("reservation/{reservationId}")]
        public async Task<ActionResult<IEnumerable<Payment>>> GetPaymentsByReservation(int reservationId)
        {
            return await _context.Payments
                .Where(p => p.ReservationId == reservationId)
                .ToListAsync();
        }

        // POST: api/Payments
        [HttpPost]
        public async Task<ActionResult<Payment>> PostPayment(Payment payment)
        {
            // Validate that the reservation exists
            var reservation = await _context.Reservations.FindAsync(payment.ReservationId);
            if (reservation == null || !reservation.IsActive)
            {
                return BadRequest("Reservation not found");
            }

            payment.PaymentDate = DateTime.UtcNow;
            payment.CreatedAt = DateTime.UtcNow;

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
        }

        // PUT: api/Payments/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutPayment(int id, Payment payment)
        {
            if (id != payment.Id)
            {
                return BadRequest();
            }

            var existingPayment = await _context.Payments.FindAsync(id);
            if (existingPayment == null)
            {
                return NotFound();
            }

            existingPayment.Amount = payment.Amount;
            existingPayment.Method = payment.Method;
            existingPayment.Note = payment.Note;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!PaymentExists(id))
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

        // DELETE: api/Payments/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePayment(int id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null)
            {
                return NotFound();
            }

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool PaymentExists(int id)
        {
            return _context.Payments.Any(e => e.Id == id);
        }
    }
}
