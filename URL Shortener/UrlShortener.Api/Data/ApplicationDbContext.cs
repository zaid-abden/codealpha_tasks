using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Data;

/// <summary>
/// Entity Framework Core database context for managing URL records.
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Database table for shortened URLs.
    /// </summary>
    public DbSet<Url> Urls => Set<Url>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Url>(entity =>
        {
            entity.HasKey(u => u.Id);

            // Short code: required, max length 16 characters, unique database index
            entity.Property(u => u.ShortCode)
                .IsRequired()
                .HasMaxLength(16);

            entity.HasIndex(u => u.ShortCode)
                .IsUnique();

            // Original URL: required, max length 2048 characters (standard max length for URLs)
            entity.Property(u => u.OriginalUrl)
                .IsRequired()
                .HasMaxLength(2048);

            // CreatedAt: required UTC timestamp
            entity.Property(u => u.CreatedAt)
                .IsRequired();
        });
    }
}
