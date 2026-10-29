using JobBoard.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Data;

public class JobBoardDbContext : DbContext
{
    public JobBoardDbContext(DbContextOptions<JobBoardDbContext> options) : base(options)
    {
    }

    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Application> Applications => Set<Application>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Job>(entity =>
        {
            entity.ToTable("Jobs");
            entity.HasKey(j => j.Id);

            entity.Property(j => j.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(j => j.Description)
                .IsRequired()
                .HasMaxLength(4000);

            entity.Property(j => j.CompanyName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(j => j.Location)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(j => j.EmploymentType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(j => j.WorkArrangement)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(j => j.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(j => j.MinimumSalary)
                .HasPrecision(18, 2);

            entity.Property(j => j.MaximumSalary)
                .HasPrecision(18, 2);

            entity.Property(j => j.CurrencyCode)
                .IsRequired()
                .HasMaxLength(3);

            entity.Property(j => j.CreatedAtUtc)
                .IsRequired();

            entity.Property(j => j.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(j => j.Status);
            entity.HasIndex(j => j.CreatedAtUtc);
            entity.HasIndex(j => j.Location);
            entity.HasIndex(j => j.EmploymentType);

            entity.HasMany(j => j.Applications)
                .WithOne(a => a.Job)
                .HasForeignKey(a => a.JobId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Application>(entity =>
        {
            entity.ToTable("Applications");
            entity.HasKey(a => a.Id);

            entity.Property(a => a.ApplicantName)
                .IsRequired()
                .HasMaxLength(150);

            entity.Property(a => a.Email)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(a => a.NormalizedEmail)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(a => a.CoverLetter)
                .HasMaxLength(4000);

            entity.Property(a => a.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(a => a.AppliedAtUtc)
                .IsRequired();

            entity.Property(a => a.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(a => a.JobId);
            entity.HasIndex(a => a.Status);
            entity.HasIndex(a => new { a.JobId, a.NormalizedEmail })
                .IsUnique();
        });
    }
}
