using EventRegistration.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Api.Data;

public class EventDbContext : DbContext
{
    public EventDbContext(DbContextOptions<EventDbContext> options) : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Registration> Registrations => Set<Registration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.ToTable("Events");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Description)
                .HasMaxLength(2000);

            entity.Property(e => e.Location)
                .IsRequired()
                .HasMaxLength(300);

            entity.Property(e => e.StartDateTime)
                .IsRequired();

            entity.Property(e => e.EndDateTime)
                .IsRequired();

            entity.Property(e => e.Capacity)
                .IsRequired();

            entity.Property(e => e.CreatedAt)
                .IsRequired();

            entity.HasIndex(e => e.StartDateTime)
                .HasDatabaseName("IX_Events_StartDateTime");
        });

        modelBuilder.Entity<Registration>(entity =>
        {
            entity.ToTable("Registrations");
            entity.HasKey(r => r.Id);

            entity.Property(r => r.ParticipantName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(r => r.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(r => r.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(r => r.RegisteredAt)
                .IsRequired();

            entity.HasIndex(r => r.NormalizedEmail)
                .HasDatabaseName("IX_Registrations_NormalizedEmail");

            entity.HasIndex(r => new { r.EventId, r.NormalizedEmail })
                .IsUnique()
                .HasDatabaseName("IX_Registrations_EventId_NormalizedEmail");

            entity.HasOne(r => r.Event)
                .WithMany(e => e.Registrations)
                .HasForeignKey(r => r.EventId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
