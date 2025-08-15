using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// 1️⃣ Load Connection String and Validate
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("❌ DefaultConnection string is missing in configuration.");
}

// 2️⃣ EF Core + SQLite
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));

// 3️⃣ Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
}

// 4️⃣ Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["ready"]);

// 5️⃣ CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllForDevelopment", cors =>
    {
        cors.AllowAnyOrigin()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("Content-Disposition");
    });
});

var app = builder.Build();

// 6️⃣ Swagger UI in Dev
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 7️⃣ HTTPS + CORS
app.UseHttpsRedirection();
app.UseCors("AllowAllForDevelopment");

// 8️⃣ Ensure Static File Directories Exist
var wwwrootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
Directory.CreateDirectory(wwwrootPath);

var imagesPath = Path.Combine(wwwrootPath, "images", "articles");
Directory.CreateDirectory(imagesPath);

// 9️⃣ Serve Static Files
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images/articles",
    ServeUnknownFileTypes = true
});

// 🔍 Debug Static Files (optional)
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

// 🔑 Auth
app.UseAuthorization();

// 1️⃣0️⃣ Controllers
app.MapControllers();

// 1️⃣1️⃣ Health Check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});

// 1️⃣2️⃣ DB Info for Debug
var dbFilePath = connectionString.Replace("Data Source=", "");
var absolutePath = Path.GetFullPath(dbFilePath);
Console.WriteLine($"📂 SQLite DB path: {absolutePath}");

// 1️⃣3️⃣ Serve Angular SPA
app.MapFallbackToFile("index.html");

// 1️⃣4️⃣ Apply EF Core Migrations on Startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
