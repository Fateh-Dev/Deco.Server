using Microsoft.EntityFrameworkCore;
using LocationDeco.API.Models;

namespace LocationDeco.API.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Article> Articles { get; set; }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<ReservationItem> ReservationItems { get; set; }
        public DbSet<Payment> Payments { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure User entity
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Phone).IsRequired().HasMaxLength(20);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.EventType).HasMaxLength(50);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure Category entity
            modelBuilder.Entity<Category>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            });

            // Configure Article entity
            modelBuilder.Entity<Article>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(500);
                entity.Property(e => e.ImageUrl).HasMaxLength(500);
                entity.Property(e => e.PricePerDay).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(e => e.Category)
                    .WithMany(c => c.Articles)
                    .HasForeignKey(e => e.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Reservation entity
            modelBuilder.Entity<Reservation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.TotalPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Status).HasConversion<string>();
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(e => e.User)
                    .WithMany(u => u.Reservations)
                    .HasForeignKey(e => e.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure ReservationItem entity
            modelBuilder.Entity<ReservationItem>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UnitPrice).HasColumnType("decimal(18,2)");
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(e => e.Reservation)
                    .WithMany(r => r.ReservationItems)
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.Cascade);
                
                entity.HasOne(e => e.Article)
                    .WithMany(a => a.ReservationItems)
                    .HasForeignKey(e => e.ArticleId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // Configure Payment entity
            modelBuilder.Entity<Payment>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Amount).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Method).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Note).HasMaxLength(500);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
                entity.HasOne(e => e.Reservation)
                    .WithMany(r => r.Payments)
                    .HasForeignKey(e => e.ReservationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed Categories
            modelBuilder.Entity<Category>().HasData(
                new Category { Id = 1, Name = "Chaises", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 2, Name = "Tables", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 3, Name = "Décorations florales", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 4, Name = "Éclairage", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 5, Name = "Miroirs", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 6, Name = "Vases", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true },
                new Category { Id = 7, Name = "Décors de table", CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsActive = true }
            );

            // Seed Articles
            modelBuilder.Entity<Article>().HasData(
                new Article
                {
                    Id = 1,
                    Name = "Chaise Chiavari dorée",
                    CategoryId = 1,
                    Description = "Chaise élégante en style Chiavari avec finition dorée",
                    QuantityTotal = 50,
                    PricePerDay = 15.00m,
                    ImageUrl = "/images/chaises/chiavari-doree.jpg",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Article
                {
                    Id = 2,
                    Name = "Table ronde 120cm",
                    CategoryId = 2,
                    Description = "Table ronde en bois massif pour 8-10 personnes",
                    QuantityTotal = 20,
                    PricePerDay = 45.00m,
                    ImageUrl = "/images/tables/ronde-120cm.jpg",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Article
                {
                    Id = 3,
                    Name = "Centre de table floral",
                    CategoryId = 3,
                    Description = "Arrangement floral pour centre de table",
                    QuantityTotal = 30,
                    PricePerDay = 25.00m,
                    ImageUrl = "/images/decorations/centre-floral.jpg",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Article
                {
                    Id = 4,
                    Name = "Miroir doré ovale",
                    CategoryId = 5,
                    Description = "Miroir décoratif ovale avec cadre doré",
                    QuantityTotal = 15,
                    PricePerDay = 35.00m,
                    ImageUrl = "/images/miroirs/ovale-dore.jpg",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                },
                new Article
                {
                    Id = 5,
                    Name = "Vase en cristal",
                    CategoryId = 6,
                    Description = "Vase en cristal transparent pour bouquets",
                    QuantityTotal = 25,
                    PricePerDay = 20.00m,
                    ImageUrl = "/images/vases/cristal.jpg",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            );

            // Seed Sample User
            modelBuilder.Entity<User>().HasData(
                new User
                {
                    Id = 1,
                    Name = "Marie Dupont",
                    Phone = "+33 6 12 34 56 78",
                    Email = "marie.dupont@email.com",
                    EventType = "Mariage",
                    CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                }
            );
        }
    }
}
