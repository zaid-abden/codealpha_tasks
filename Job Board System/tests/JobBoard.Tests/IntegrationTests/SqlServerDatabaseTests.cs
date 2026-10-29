using JobBoard.Api.Data;
using JobBoard.Api.Models;
using JobBoard.Api.Models.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Tests.IntegrationTests;

public class SqlServerDatabaseTests
{
    private const string LocalDbConnectionString = "Server=(localdb)\\mssqllocaldb;Database=JobBoardDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static bool IsLocalDbAvailable()
    {
        try
        {
            using var connection = new SqlConnection(LocalDbConnectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task SqlServer_EnforcesUniqueConstraint_OnJobIdAndNormalizedEmail()
    {
        if (!IsLocalDbAvailable())
        {
            return; // Skip if LocalDB is not reachable in current context
        }

        var options = new DbContextOptionsBuilder<JobBoardDbContext>()
            .UseSqlServer(LocalDbConnectionString)
            .Options;

        using var context = new JobBoardDbContext(options);

        var testJob = new Job
        {
            Title = $"SqlServer Test Job {Guid.NewGuid():N}",
            Description = "Testing unique constraint directly in SQL Server engine.",
            CompanyName = "SQL Test Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            Status = JobStatus.Published,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.Jobs.Add(testJob);
        await context.SaveChangesAsync();

        var email = $"candidate_{Guid.NewGuid():N}@test.com";
        var app1 = new Application
        {
            JobId = testJob.Id,
            ApplicantName = "Candidate One",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            Status = ApplicationStatus.Submitted,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.Applications.Add(app1);
        await context.SaveChangesAsync();

        // Attempt second insert with identical JobId and NormalizedEmail
        var app2 = new Application
        {
            JobId = testJob.Id,
            ApplicantName = "Candidate Two",
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            Status = ApplicationStatus.Submitted,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        context.Applications.Add(app2);

        // SQL Server must throw DbUpdateException due to unique index violation (IX_Applications_JobId_NormalizedEmail)
        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.NotNull(ex.InnerException);
        Assert.Contains("IX_Applications_JobId_NormalizedEmail", ex.InnerException.Message);

        // Cleanup
        context.ChangeTracker.Clear();
        var apps = context.Applications.Where(a => a.JobId == testJob.Id);
        context.Applications.RemoveRange(apps);
        var job = await context.Jobs.FindAsync(testJob.Id);
        if (job != null) context.Jobs.Remove(job);
        await context.SaveChangesAsync();
    }
}
