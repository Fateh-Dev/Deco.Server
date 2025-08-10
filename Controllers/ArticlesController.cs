using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;

namespace LocationDeco.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArticlesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ArticlesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/Articles
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticles()
        {
            return await _context.Articles
                .Include(a => a.Category)
                .Where(a => a.IsActive)
                .ToListAsync();
        }

        // GET: api/Articles/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Article>> GetArticle(int id)
        {
            var article = await _context.Articles
                .Include(a => a.Category)
                .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);

            if (article == null)
            {
                return NotFound();
            }

            return article;
        }

        // GET: api/Articles/category/5
        [HttpGet("category/{categoryId}")]
        public async Task<ActionResult<IEnumerable<Article>>> GetArticlesByCategory(int categoryId)
        {
            return await _context.Articles
                .Include(a => a.Category)
                .Where(a => a.CategoryId == categoryId && a.IsActive)
                .ToListAsync();
        }

        // POST: api/Articles
        [HttpPost]
        public async Task<ActionResult<Article>> PostArticle(Article article)
        {
            try
            {
                // Log the incoming article data
                Console.WriteLine($"Received article: {System.Text.Json.JsonSerializer.Serialize(article)}");

                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    Console.WriteLine($"Model validation errors: {string.Join(", ", errors)}");
                    return BadRequest(new { message = "Validation failed", errors });
                }

                // Ensure required fields are set
                article.CreatedAt = DateTime.UtcNow;
                article.IsActive = true;
                
                // Ensure Category exists
                var categoryExists = await _context.Categories.AnyAsync(c => c.Id == article.CategoryId);
                if (!categoryExists)
                {
                    return BadRequest(new { message = "Invalid CategoryId specified" });
                }
                
                _context.Articles.Add(article);
                await _context.SaveChangesAsync();

                return CreatedAtAction(nameof(GetArticle), new { id = article.Id }, article);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating article: {ex}");
                return StatusCode(500, new { message = "An error occurred while creating the article", error = ex.Message });
            }
        }

        // PUT: api/Articles/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutArticle(int id, Article article)
        {
            if (id != article.Id)
            {
                return BadRequest();
            }

            var existingArticle = await _context.Articles.FindAsync(id);
            if (existingArticle == null || !existingArticle.IsActive)
            {
                return NotFound();
            }

            article.CreatedAt = existingArticle.CreatedAt;
            article.IsActive = true;

            _context.Entry(existingArticle).CurrentValues.SetValues(article);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ArticleExists(id))
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

        // DELETE: api/Articles/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            var article = await _context.Articles.FindAsync(id);
            if (article == null || !article.IsActive)
            {
                return NotFound();
            }

            article.IsActive = false;
            
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool ArticleExists(int id)
        {
            return _context.Articles.Any(e => e.Id == id && e.IsActive);
        }
    }
}
