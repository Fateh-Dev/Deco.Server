using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Data;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// ✅ Allow running as a Windows Service
builder.Host.UseWindowsService();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerGen();
}

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["ready"]);

// ✅ Correct SQLite DB path
var dbPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "LocationDeco.db");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAllForDevelopment");

// ✅ Ensure folders exist
var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
Directory.CreateDirectory(wwwrootPath);

var imagesPath = Path.Combine(wwwrootPath, "images", "articles");
Directory.CreateDirectory(imagesPath);

// ✅ Serve static files
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imagesPath),
    RequestPath = "/images/articles",
    ServeUnknownFileTypes = true
});

app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("ready")
});
app.MapFallbackToFile("index.html");

// ✅ Apply migrations at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

app.Run();
