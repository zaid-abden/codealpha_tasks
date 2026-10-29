using JobBoard.Api.Data;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Exceptions;
using JobBoard.Api.Models;
using JobBoard.Api.Models.Enums;
using JobBoard.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Tests.UnitTests;

public class JobServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly JobBoardDbContext _context;
    private readonly JobService _jobService;

    public JobServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<JobBoardDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new JobBoardDbContext(options);
        _context.Database.EnsureCreated();

        _jobService = new JobService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateJobAsync_SetsInitialStatusToDraft_AndPopulatesTimestamps()
    {
        var dto = new JobCreateDto
        {
            Title = "Backend Engineer",
            Description = "Join our high-performance engineering team.",
            CompanyName = "Acme Corp",
            Location = "Cairo, Egypt",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            MinimumSalary = 20000,
            MaximumSalary = 30000
        };

        var result = await _jobService.CreateJobAsync(dto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(JobStatus.Draft, result.Status);
        Assert.Equal("Backend Engineer", result.Title);
        Assert.True((DateTime.UtcNow - result.CreatedAtUtc).TotalSeconds < 5);
        Assert.True((DateTime.UtcNow - result.UpdatedAtUtc).TotalSeconds < 5);
    }

    [Fact]
    public async Task CreateJobAsync_WithMaxSalaryLessThanMinSalary_ThrowsValidationException()
    {
        var dto = new JobCreateDto
        {
            Title = "Backend Engineer",
            Description = "Join our team.",
            CompanyName = "Acme Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            MinimumSalary = 50000,
            MaximumSalary = 30000 // Less than min
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _jobService.CreateJobAsync(dto));
        Assert.Contains("Maximum salary must be greater than or equal to minimum salary", ex.Message);
    }

    [Fact]
    public async Task CreateJobAsync_WithPastDeadline_ThrowsValidationException()
    {
        var dto = new JobCreateDto
        {
            Title = "Backend Engineer",
            Description = "Join our team.",
            CompanyName = "Acme Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            ApplicationDeadlineUtc = DateTime.UtcNow.AddDays(-2) // Past
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _jobService.CreateJobAsync(dto));
        Assert.Contains("future date", ex.Message);
    }

    [Fact]
    public async Task PublishJobAsync_WhenDraftAndValid_Succeeds()
    {
        var job = new Job
        {
            Title = "DevOps Engineer",
            Description = "Cloud infrastructure engineer.",
            CompanyName = "Cloud Tech",
            Location = "Alexandria",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            Status = JobStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ApplicationDeadlineUtc = DateTime.UtcNow.AddMonths(1)
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var result = await _jobService.PublishJobAsync(job.Id);

        Assert.Equal(JobStatus.Published, result.Status);
        var updatedJob = await _context.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Published, updatedJob!.Status);
    }

    [Fact]
    public async Task PublishJobAsync_WhenAlreadyPublished_ThrowsConflictException()
    {
        var job = new Job
        {
            Title = "DevOps Engineer",
            Description = "Cloud infrastructure engineer.",
            CompanyName = "Cloud Tech",
            Location = "Alexandria",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            Status = JobStatus.Published,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _jobService.PublishJobAsync(job.Id));
        Assert.Contains("Only draft jobs can be published", ex.Message);
    }

    [Fact]
    public async Task PublishJobAsync_WhenDeadlineHasPassed_ThrowsConflictException()
    {
        var job = new Job
        {
            Title = "DevOps Engineer",
            Description = "Cloud infrastructure engineer.",
            CompanyName = "Cloud Tech",
            Location = "Alexandria",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            Status = JobStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow.AddDays(-10),
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-10),
            ApplicationDeadlineUtc = DateTime.UtcNow.AddDays(-1) // Passed
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _jobService.PublishJobAsync(job.Id));
        Assert.Contains("expired application deadline", ex.Message);
    }

    [Fact]
    public async Task CloseJobAsync_WhenPublished_TransitionsToClosed()
    {
        var job = new Job
        {
            Title = "Frontend Engineer",
            Description = "Angular/React developer.",
            CompanyName = "Web Studio",
            Location = "Giza",
            EmploymentType = EmploymentType.Contract,
            WorkArrangement = WorkArrangement.Hybrid,
            Status = JobStatus.Published,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var result = await _jobService.CloseJobAsync(job.Id);

        Assert.Equal(JobStatus.Closed, result.Status);
        var updated = await _context.Jobs.FindAsync(job.Id);
        Assert.Equal(JobStatus.Closed, updated!.Status);
    }

    [Fact]
    public async Task CloseJobAsync_WhenDraft_ThrowsConflictException()
    {
        var job = new Job
        {
            Title = "Frontend Engineer",
            Description = "Angular/React developer.",
            CompanyName = "Web Studio",
            Location = "Giza",
            EmploymentType = EmploymentType.Contract,
            WorkArrangement = WorkArrangement.Hybrid,
            Status = JobStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _jobService.CloseJobAsync(job.Id));
        Assert.Contains("Only published jobs can be closed", ex.Message);
    }

    [Fact]
    public async Task UpdateJobAsync_WhenJobIsClosed_ThrowsConflictException()
    {
        var job = new Job
        {
            Title = "QA Tester",
            Description = "Automation engineer.",
            CompanyName = "Quality First",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.OnSite,
            Status = JobStatus.Closed,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var updateDto = new JobUpdateDto
        {
            Title = "QA Lead",
            Description = "Lead QA engineer.",
            CompanyName = "Quality First",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.OnSite
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _jobService.UpdateJobAsync(job.Id, updateDto));
        Assert.Contains("Cannot update a job that is in 'Closed' status", ex.Message);
    }

    [Fact]
    public async Task DeleteJobAsync_WhenJobHasApplications_PreservesHistoryByArchiving()
    {
        var job = new Job
        {
            Title = "Data Scientist",
            Description = "Python and ML expert.",
            CompanyName = "Data Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            Status = JobStatus.Published,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        var application = new Application
        {
            JobId = job.Id,
            ApplicantName = "Sara Tarek",
            Email = "sara@example.com",
            NormalizedEmail = "SARA@EXAMPLE.COM",
            Status = ApplicationStatus.Submitted,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Applications.Add(application);
        await _context.SaveChangesAsync();

        await _jobService.DeleteJobAsync(job.Id);

        var archivedJob = await _context.Jobs.FindAsync(job.Id);
        Assert.NotNull(archivedJob);
        Assert.Equal(JobStatus.Archived, archivedJob.Status);

        var appCount = await _context.Applications.CountAsync(a => a.JobId == job.Id);
        Assert.Equal(1, appCount); // Applications history preserved
    }

    [Fact]
    public async Task DeleteJobAsync_WhenJobHasNoApplications_HardDeletesJob()
    {
        var job = new Job
        {
            Title = "Intern Developer",
            Description = "Eager learner.",
            CompanyName = "Startup",
            Location = "Cairo",
            EmploymentType = EmploymentType.Internship,
            WorkArrangement = WorkArrangement.Hybrid,
            Status = JobStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();

        await _jobService.DeleteJobAsync(job.Id);

        var deletedJob = await _context.Jobs.FindAsync(job.Id);
        Assert.Null(deletedJob);
    }

    [Fact]
    public async Task GetPublishedJobsAsync_ExcludesDraft_Closed_Archived_AndExpiredJobs()
    {
        var now = DateTime.UtcNow;

        _context.Jobs.AddRange(
            new Job { Title = "Published Active", Description = "Active desc", CompanyName = "Comp A", Location = "Cairo", Status = JobStatus.Published, CreatedAtUtc = now, UpdatedAtUtc = now },
            new Job { Title = "Draft Job", Description = "Draft desc", CompanyName = "Comp B", Location = "Cairo", Status = JobStatus.Draft, CreatedAtUtc = now, UpdatedAtUtc = now },
            new Job { Title = "Closed Job", Description = "Closed desc", CompanyName = "Comp C", Location = "Cairo", Status = JobStatus.Closed, CreatedAtUtc = now, UpdatedAtUtc = now },
            new Job { Title = "Archived Job", Description = "Archived desc", CompanyName = "Comp D", Location = "Cairo", Status = JobStatus.Archived, CreatedAtUtc = now, UpdatedAtUtc = now },
            new Job { Title = "Expired Job", Description = "Expired desc", CompanyName = "Comp E", Location = "Cairo", Status = JobStatus.Published, CreatedAtUtc = now, UpdatedAtUtc = now, ApplicationDeadlineUtc = now.AddDays(-2) }
        );
        await _context.SaveChangesAsync();

        var filter = new JobFilterDto();
        var result = await _jobService.GetPublishedJobsAsync(filter);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("Published Active", result.Items[0].Title);
    }
}
