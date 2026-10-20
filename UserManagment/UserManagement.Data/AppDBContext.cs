using Microsoft.EntityFrameworkCore;

namespace UserManagement.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(u => u.Id);

                // Ограничения полей (запрет пустых значений)
                entity.Property(u => u.Name).IsRequired().HasMaxLength(100);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
                entity.Property(u => u.Password).IsRequired().HasMaxLength(255);
                entity.Property(u => u.CreatedAt).IsRequired();
                entity.Property(u => u.IsActive).IsRequired().HasDefaultValue(true);

                // Бизнес-правила: уникальность Name и Email
                entity.HasIndex(u => u.Name).IsUnique();
                entity.HasIndex(u => u.Email).IsUnique();

                // Стартовые тестовые пользователи (запишутся при создании БД)
                entity.HasData(
                    new User
                    {
                        Id = 1,
                        Name = "admin",
                        Email = "admin@example.com",
                        Password = "admin_password_123",
                        CreatedAt = new DateTime(2026, 1, 1, 10, 0, 0),
                        IsActive = true
                    },
                    new User
                    {
                        Id = 2,
                        Name = "ivan_petrov",
                        Email = "ivan@example.com",
                        Password = "ivan_password_456",
                        CreatedAt = new DateTime(2026, 1, 2, 14, 30, 0),
                        IsActive = true
                    }
                );
            });
        }
    }
}