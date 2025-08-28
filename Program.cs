using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.IO;
using LocationDeco.API.auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using LocationDeco.API.Models;

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

// Register AuthService
builder.Services.AddSingleton<AuthService>();

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

// 3️⃣ Controllers + Swagger
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
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
app.UseAuthentication();
app.UseAuthorization();

// 1️⃣0️⃣ Controllers
app.MapControllers();

// 1️⃣1️⃣ Health Check
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
app.MapGet("/", () => "Hello from .NET on Koyeb!");

// 1️⃣2️⃣ DB Info for Debug
var dbFilePath = connectionString.Replace("Data Source=", "");
var absolutePath = Path.GetFullPath(dbFilePath);
Console.WriteLine($"📂 SQLite DB path: {absolutePath}");

// 1️⃣3️⃣ Serve Angular SPA
app.MapFallbackToFile("index.html");

// 1️⃣4️⃣ Apply EF Core Migrations on Startup - FIXED VERSION
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    try
    {
        logger.LogInformation("🔄 Initializing database...");

        // Ensure database directory exists
        var dbDir = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(dbDir) && !Directory.Exists(dbDir))
        {
            Directory.CreateDirectory(dbDir);
            logger.LogInformation($"📁 Created database directory: {dbDir}");
        }

        // Create database and tables if they don't exist
        var created = await db.Database.EnsureCreatedAsync();
        if (created)
        {
            logger.LogInformation("✅ Database created successfully");
        }
        else
        {
            logger.LogInformation("ℹ️ Database already exists");
        }

        // Alternative: Use migrations if you have them
        // await db.Database.MigrateAsync();

        // Seed default admin user
        var authService = scope.ServiceProvider.GetRequiredService<AuthService>();

        // Check if Users table exists and has any admin user
        var hasAdminUser = await db.Users.AnyAsync(u => u.Name == "admin");

        if (!hasAdminUser)
        {
            logger.LogInformation("👤 Creating default admin user...");

            authService.CreatePasswordHash("123456**-", out var hash, out var salt);
            db.Users.Add(new User
            {
                Name = "admin",
                Email = "admin@admin.com",
                Phone = "0000000000",
                PasswordHash = hash,
                PasswordSalt = salt,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
            logger.LogInformation("✅ Admin user created successfully");
        }
        else
        {
            logger.LogInformation("ℹ️ Admin user already exists");
        }

        logger.LogInformation("🚀 Database initialization completed successfully");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Error initializing database");
        throw; // Re-throw to prevent startup with broken database
    }
}

app.Run("http://0.0.0.0:5000");