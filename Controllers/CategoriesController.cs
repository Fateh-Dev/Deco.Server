using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;

namespace LocationDeco.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CategoriesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Categories
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Category>>> GetCategories()
        {
            return await _context.Categories
                .Where(c => c.IsActive)
                .ToListAsync();
        }

        // GET: api/Categories/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Category>> GetCategory(int id)
        {
            var category = await _context.Categories
                .Include(c => c.Articles.Where(a => a.IsActive))
                .FirstOrDefaultAsync(c => c.Id == id && c.IsActive);

            if (category == null)
            {
                return NotFound();
            }

            return category;
        }

        // POST: api/Categories
        [HttpPost]
        public async Task<ActionResult<Category>> PostCategory(Category category)
        {
            category.CreatedAt = DateTime.UtcNow;
            category.IsActive = true;
            
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCategory), new { id = category.Id }, category);
        }

        // PUT: api/Categories/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutCategory(int id, Category category)
        {
            if (id != category.Id)
            {
                return BadRequest();
            }

            var existingCategory = await _context.Categories.FindAsync(id);
            if (existingCategory == null || !existingCategory.IsActive)
            {
                return NotFound();
            }

            category.CreatedAt = existingCategory.CreatedAt;
            category.IsActive = true;

            _context.Entry(existingCategory).CurrentValues.SetValues(category);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(id))
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

        // DELETE: api/Categories/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.Categories.FindAsync(id);
            if (category == null || !category.IsActive)
            {
                return NotFound();
            }

            category.IsActive = false;
            
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool CategoryExists(int id)
        {
            return _context.Categories.Any(e => e.Id == id && e.IsActive);
        }
    }
}
