using JobBoard.Api.Data;
using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.Exceptions;
using JobBoard.Api.Models;
using JobBoard.Api.Models.Enums;
using JobBoard.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JobBoard.Tests.UnitTests;

public class ApplicationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly JobBoardDbContext _context;
    private readonly ApplicationService _applicationService;

    public ApplicationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<JobBoardDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new JobBoardDbContext(options);
        _context.Database.EnsureCreated();

        _applicationService = new ApplicationService(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private async Task<Job> CreateTestJob(JobStatus status = JobStatus.Published, DateTime? deadline = null)
    {
        var job = new Job
        {
            Title = "Senior Backend Engineer",
            Description = "Building scalable cloud microservices.",
            CompanyName = "Global Inc",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            Status = status,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            ApplicationDeadlineUtc = deadline
        };
        _context.Jobs.Add(job);
        await _context.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task ApplyForJobAsync_WithValidDetails_SubmitsApplicationSuccessfully()
    {
        var job = await CreateTestJob(JobStatus.Published);

        var dto = new ApplicationCreateDto
        {
            ApplicantName = "Kareem Hassan",
            Email = "kareem.hassan@example.com",
            CoverLetter = "Strong C# background."
        };

        var result = await _applicationService.ApplyForJobAsync(job.Id, dto);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal(job.Id, result.JobId);
        Assert.Equal("Kareem Hassan", result.ApplicantName);
        Assert.Equal("kareem.hassan@example.com", result.Email);
        Assert.Equal(ApplicationStatus.Submitted, result.Status);
    }

    [Fact]
    public async Task ApplyForJobAsync_DuplicateApplicationWithDifferentCasing_ThrowsConflictException()
    {
        var job = await CreateTestJob(JobStatus.Published);

        var firstDto = new ApplicationCreateDto
        {
            ApplicantName = "Kareem Hassan",
            Email = "kareem.hassan@example.com"
        };
        await _applicationService.ApplyForJobAsync(job.Id, firstDto);

        var duplicateDto = new ApplicationCreateDto
        {
            ApplicantName = "Kareem Hassan",
            Email = "  KAREEM.HASSAN@EXAMPLE.COM  " // Different casing and spaces
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _applicationService.ApplyForJobAsync(job.Id, duplicateDto));

        Assert.Contains("already been submitted", ex.Message);
    }

    [Fact]
    public async Task ApplyForJobAsync_WhenJobIsClosed_ThrowsConflictException()
    {
        var job = await CreateTestJob(JobStatus.Closed);

        var dto = new ApplicationCreateDto
        {
            ApplicantName = "Mona Adel",
            Email = "mona.adel@example.com"
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _applicationService.ApplyForJobAsync(job.Id, dto));

        Assert.Contains("closed", ex.Message);
    }

    [Fact]
    public async Task ApplyForJobAsync_WhenDeadlineHasPassed_ThrowsConflictException()
    {
        var job = await CreateTestJob(JobStatus.Published, deadline: DateTime.UtcNow.AddDays(-1));

        var dto = new ApplicationCreateDto
        {
            ApplicantName = "Mona Adel",
            Email = "mona.adel@example.com"
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _applicationService.ApplyForJobAsync(job.Id, dto));

        Assert.Contains("deadline", ex.Message);
    }

    [Fact]
    public async Task ApplyForJobAsync_WhenJobIsDraft_ThrowsNotFoundException()
    {
        var job = await CreateTestJob(JobStatus.Draft);

        var dto = new ApplicationCreateDto
        {
            ApplicantName = "Mona Adel",
            Email = "mona.adel@example.com"
        };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _applicationService.ApplyForJobAsync(job.Id, dto));

        Assert.Contains("not publicly available", ex.Message);
    }

    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.UnderReview, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Hired, false)] // Cannot skip directly to Hired
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Shortlisted, true)]
    [InlineData(ApplicationStatus.UnderReview, ApplicationStatus.Rejected, true)]
    [InlineData(ApplicationStatus.Shortlisted, ApplicationStatus.Hired, true)]
    [InlineData(ApplicationStatus.Hired, ApplicationStatus.UnderReview, false)] // Terminal
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.UnderReview, false)] // Terminal
    [InlineData(ApplicationStatus.Withdrawn, ApplicationStatus.Submitted, false)] // Terminal
    public async Task UpdateApplicationStatusAsync_ValidatesTransitions(
        ApplicationStatus current, ApplicationStatus target, bool shouldSucceed)
    {
        var job = await CreateTestJob();
        var app = new Application
        {
            JobId = job.Id,
            ApplicantName = "Test Applicant",
            Email = "test@example.com",
            NormalizedEmail = "TEST@EXAMPLE.COM",
            Status = current,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Applications.Add(app);
        await _context.SaveChangesAsync();

        var updateDto = new ApplicationStatusUpdateDto { Status = target };

        if (shouldSucceed)
        {
            var result = await _applicationService.UpdateApplicationStatusAsync(app.Id, updateDto);
            Assert.Equal(target, result.Status);
        }
        else
        {
            var ex = await Assert.ThrowsAsync<ConflictException>(() =>
                _applicationService.UpdateApplicationStatusAsync(app.Id, updateDto));
            Assert.Contains("Cannot transition application status", ex.Message);
        }
    }

    [Fact]
    public async Task WithdrawApplicationAsync_WhenSubmitted_TransitionsToWithdrawn()
    {
        var job = await CreateTestJob();
        var app = new Application
        {
            JobId = job.Id,
            ApplicantName = "Youssef Nader",
            Email = "youssef@example.com",
            NormalizedEmail = "YOUSSEF@EXAMPLE.COM",
            Status = ApplicationStatus.Submitted,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Applications.Add(app);
        await _context.SaveChangesAsync();

        var result = await _applicationService.WithdrawApplicationAsync(app.Id);

        Assert.Equal(ApplicationStatus.Withdrawn, result.Status);
        var updated = await _context.Applications.FindAsync(app.Id);
        Assert.Equal(ApplicationStatus.Withdrawn, updated!.Status);
    }

    [Theory]
    [InlineData(ApplicationStatus.Hired)]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public async Task WithdrawApplicationAsync_WhenInTerminalState_ThrowsConflictException(ApplicationStatus terminalStatus)
    {
        var job = await CreateTestJob();
        var app = new Application
        {
            JobId = job.Id,
            ApplicantName = "Youssef Nader",
            Email = "youssef@example.com",
            NormalizedEmail = "YOUSSEF@EXAMPLE.COM",
            Status = terminalStatus,
            AppliedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        _context.Applications.Add(app);
        await _context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _applicationService.WithdrawApplicationAsync(app.Id));
    }
}
