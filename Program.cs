using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using Microsoft.Extensions.FileProviders;
using System.IO;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure Swagger only in Development
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
}

// Add health checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["ready"]);

// Add Entity Framework with SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add CORS for development
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllForDevelopment",
        builder =>
        {
            builder.AllowAnyOrigin()
                   .AllowAnyMethod()
                   .AllowAnyHeader()
                   .WithExposedHeaders("Content-Disposition");
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Use CORS
app.UseCors("AllowAllForDevelopment");

// Set up wwwroot path and ensure it exists
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
if (!Directory.Exists(wwwrootPath))
{
    Directory.CreateDirectory(wwwrootPath);
}

// Set up images path and ensure it exists
var imagesPath = Path.Combine(wwwrootPath, "images", "articles");
if (!Directory.Exists(imagesPath))
{
    Directory.CreateDirectory(imagesPath);
}

// Enable serving of all static files from wwwroot
app.UseStaticFiles();

// Add a specific mapping for /images/articles to serve from the images directory
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images/articles",
    ServeUnknownFileTypes = true // This ensures all file types are served
});

// Add a catch-all route to help debug static file serving
app.MapGet("/debug/files", () => 
{
    var files = Directory.GetFiles(imagesPath, "*.*", SearchOption.AllDirectories)
        .Select(f => new 
        { 
            Path = f.Replace(imagesPath, "").Replace("\\", "/"),
            Exists = true
        });
    return Results.Ok(files);
});

app.UseAuthorization();

app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

// Simple health check endpoint for Render
app.MapGet("/", () => "LocationDeco API is running!");

// Apply database migrations and ensure database is created
using (var scope = app.Services.CreateScope())
{
    try
    {
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
