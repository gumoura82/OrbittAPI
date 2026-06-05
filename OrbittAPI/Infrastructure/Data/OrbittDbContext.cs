using Microsoft.EntityFrameworkCore;
using OrbittAPI.Domain.Entities;

namespace OrbittAPI.Infrastructure.Data;

public class OrbittDbContext : DbContext
{
    public OrbittDbContext(DbContextOptions<OrbittDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<ApiCall> ApiCalls => Set<ApiCall>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.Name).HasMaxLength(200).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        // ApiKey
        modelBuilder.Entity<ApiKey>(e =>
        {
            e.HasKey(k => k.Id);
            e.HasIndex(k => k.KeyValue).IsUnique();
            e.Property(k => k.KeyValue).HasMaxLength(100).IsRequired();
            e.HasOne(k => k.User)
             .WithMany(u => u.ApiKeys)
             .HasForeignKey(k => k.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ApiCall
        modelBuilder.Entity<ApiCall>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Endpoint).HasMaxLength(100).IsRequired();
            e.HasIndex(c => new { c.UserId, c.CalledAt });
            e.HasOne(c => c.User)
             .WithMany(u => u.ApiCalls)
             .HasForeignKey(c => c.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
