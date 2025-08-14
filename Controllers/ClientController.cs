using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;

namespace LocationDeco.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ClientController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Client
        // Now supports search with query parameters
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Client>>> GetClients([FromQuery] string? search = null)
        {
            var query = _context.Clients.Where(c => c.IsActive);

            // Apply search filter if search term is provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                query = query.Where(c => 
                    c.Name.ToLower().Contains(searchTerm) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(searchTerm)) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchTerm)) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
                    (c.Address != null && c.Address.ToLower().Contains(searchTerm))
                );
            }

            return await query.OrderBy(c => c.Name).ToListAsync();
        } 

        // GET: api/Client/search/{term} - Alternative search endpoint
        [HttpGet("search/{term}")]
        public async Task<ActionResult<IEnumerable<Client>>> SearchClients(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
            {
                return await GetClients();
            }

            var searchTerm = term.Trim().ToLower();
            var clients = await _context.Clients
                .Where(c => c.IsActive && (
                    c.Name.ToLower().Contains(searchTerm) ||
                    (c.Phone != null && c.Phone.ToLower().Contains(searchTerm)) ||
                    (c.Email != null && c.Email.ToLower().Contains(searchTerm)) ||
                    (c.CompanyName != null && c.CompanyName.ToLower().Contains(searchTerm)) ||
                    (c.Address != null && c.Address.ToLower().Contains(searchTerm))
                ))
                .OrderBy(c => c.Name)
                .ToListAsync();

            return clients;
        }

        // GET: api/Client/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Client>> GetClient(int id)
        {
            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (client == null)
            {
                return NotFound();
            }

            return client;
        }

        // POST: api/Client
        [HttpPost]
        public async Task<ActionResult<Client>> PostClient(Client client)
        {
            client.CreatedAt = DateTime.UtcNow;
            client.IsActive = true;
            
            _context.Clients.Add(client);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClient), new { id = client.Id }, client);
        }

        // PUT: api/Client/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutClient(int id, Client client)
        {
            if (id != client.Id)
            {
                return BadRequest();
            }

            var existingClient = await _context.Clients.FindAsync(id);
            if (existingClient == null || !existingClient.IsActive)
            {
                return NotFound();
            }

            client.CreatedAt = existingClient.CreatedAt;
            client.IsActive = true;

            _context.Entry(existingClient).CurrentValues.SetValues(client);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ClientExists(id))
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

        // DELETE: api/Client/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _context.Clients.FindAsync(id);
            if (client == null || !client.IsActive)
            {
                return NotFound();
            }

            client.IsActive = false;
            
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.Id == id && e.IsActive);
        }
    }
}