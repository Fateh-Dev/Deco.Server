using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using LocationDeco.API.Models;
using Microsoft.AspNetCore.StaticFiles;
using System.IO;
using System.Web;

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
                // Console.WriteLine($"Received article: {System.Text.Json.JsonSerializer.Serialize(article)}");

                // Validate model state
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage);
                    
                    // Console.WriteLine($"Model validation errors: {string.Join(", ", errors)}");
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
                // Console.WriteLine($"Error creating article: {ex}");
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

        // POST: api/Articles/upload
        [HttpPost("upload"), DisableRequestSizeLimit]
        public async Task<IActionResult> UploadImage()
        {
            try
            {
                Console.WriteLine("UploadImage endpoint hit");
                
                if (Request.Form.Files == null || Request.Form.Files.Count == 0)
                {
                    Console.WriteLine("No files found in the request");
                    return BadRequest(new { message = "No file was uploaded" });
                }

                var file = Request.Form.Files[0];
                if (file == null || file.Length == 0)
                {
                    Console.WriteLine("File is empty or null");
                    return BadRequest(new { message = "The uploaded file is empty" });
                }

                Console.WriteLine($"Received file: {file.FileName}, Size: {file.Length} bytes");

                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
                {
                    Console.WriteLine($"Invalid file extension: {extension}");
                    return BadRequest(new { message = "Invalid file type. Only JPG, JPEG, PNG, and GIF are allowed." });
                }

                // Create a unique file name
                var fileName = $"{Guid.NewGuid()}{extension}";
                var imagesPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "articles");
                
                // Ensure the directory exists
                if (!Directory.Exists(imagesPath))
                {
                    Console.WriteLine($"Creating directory: {imagesPath}");
                    Directory.CreateDirectory(imagesPath);
                }
                
                var filePath = Path.Combine(imagesPath, fileName);
                Console.WriteLine($"Saving file to: {filePath}");

                // Save the file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Return the file URL
                var fileUrl = $"/images/articles/{fileName}";
                var fullUrl = $"{Request.Scheme}://{Request.Host}{fileUrl}";
                Console.WriteLine($"File uploaded successfully. URL: {fileUrl}");
                return Ok(new { 
                    fileUrl = fileUrl,
                    fullUrl = fullUrl
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }
}
