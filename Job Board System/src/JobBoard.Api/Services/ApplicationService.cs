using JobBoard.Api.Data;
using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.Exceptions;
using JobBoard.Api.Models;
using JobBoard.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Services;

public class ApplicationService : IApplicationService
{
    private readonly JobBoardDbContext _context;

    // Allowed status transitions
    private static readonly Dictionary<ApplicationStatus, HashSet<ApplicationStatus>> AllowedTransitions = new()
    {
        [ApplicationStatus.Submitted] = new() { ApplicationStatus.UnderReview, ApplicationStatus.Rejected },
        [ApplicationStatus.UnderReview] = new() { ApplicationStatus.Shortlisted, ApplicationStatus.Rejected },
        [ApplicationStatus.Shortlisted] = new() { ApplicationStatus.Hired, ApplicationStatus.Rejected, ApplicationStatus.UnderReview },
        [ApplicationStatus.Rejected] = new(),
        [ApplicationStatus.Hired] = new(),
        [ApplicationStatus.Withdrawn] = new()
    };

    public ApplicationService(JobBoardDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationResponseDto> ApplyForJobAsync(int jobId, ApplicationCreateDto dto, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job == null)
        {
            throw new NotFoundException($"Job with ID {jobId} was not found.");
        }

        if (job.Status == JobStatus.Closed)
        {
            throw new ConflictException("The job is closed and is no longer accepting applications.");
        }

        if (job.Status != JobStatus.Published)
        {
            throw new NotFoundException($"Job with ID {jobId} was not found or is not publicly available.");
        }

        if (job.ApplicationDeadlineUtc.HasValue && job.ApplicationDeadlineUtc.Value <= DateTime.UtcNow)
        {
            throw new ConflictException("The application deadline for this job has passed.");
        }

        var normalizedEmail = dto.Email.Trim().ToUpperInvariant();

        var existingApplication = await _context.Applications
            .AsNoTracking()
            .AnyAsync(a => a.JobId == jobId && a.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existingApplication)
        {
            throw new ConflictException($"An application for '{dto.Email}' has already been submitted for this job.");
        }

        var now = DateTime.UtcNow;
        var application = new Application
        {
            JobId = jobId,
            ApplicantName = dto.ApplicantName.Trim(),
            Email = dto.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            CoverLetter = string.IsNullOrWhiteSpace(dto.CoverLetter) ? null : dto.CoverLetter.Trim(),
            Status = ApplicationStatus.Submitted,
            AppliedAtUtc = now,
            UpdatedAtUtc = now
        };

        try
        {
            _context.Applications.Add(application);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Catch database unique constraint violation (JobId, NormalizedEmail)
            throw new ConflictException($"An application for '{dto.Email}' has already been submitted for this job.");
        }

        return MapToDto(application);
    }

    public async Task<PagedResultDto<ApplicationResponseDto>> GetApplicationsForJobAsync(int jobId, PaginationQueryDto pagination, CancellationToken cancellationToken = default)
    {
        var jobExists = await _context.Jobs.AnyAsync(j => j.Id == jobId, cancellationToken);
        if (!jobExists)
        {
            throw new NotFoundException($"Job with ID {jobId} was not found.");
        }

        var query = _context.Applications
            .AsNoTracking()
            .Where(a => a.JobId == jobId)
            .OrderByDescending(a => a.AppliedAtUtc);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = pagination.PageNumber < 1 ? 1 : pagination.PageNumber;
        var pageSize = pagination.PageSize < 1 ? 10 : (pagination.PageSize > 50 ? 50 : pagination.PageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => MapToDto(a))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<ApplicationResponseDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<ApplicationResponseDto> GetApplicationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

        if (application == null)
        {
            throw new NotFoundException($"Application with ID {id} was not found.");
        }

        return MapToDto(application);
    }

    public async Task<ApplicationResponseDto> UpdateApplicationStatusAsync(int id, ApplicationStatusUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application == null)
        {
            throw new NotFoundException($"Application with ID {id} was not found.");
        }

        if (application.Status == dto.Status)
        {
            return MapToDto(application);
        }

        if (!AllowedTransitions.TryGetValue(application.Status, out var allowedNextStates) ||
            !allowedNextStates.Contains(dto.Status))
        {
            throw new ConflictException($"Cannot transition application status from '{application.Status}' to '{dto.Status}'.");
        }

        application.Status = dto.Status;
        application.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(application);
    }

    public async Task<ApplicationResponseDto> WithdrawApplicationAsync(int id, CancellationToken cancellationToken = default)
    {
        var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (application == null)
        {
            throw new NotFoundException($"Application with ID {id} was not found.");
        }

        if (application.Status == ApplicationStatus.Withdrawn)
        {
            throw new ConflictException("The application has already been withdrawn.");
        }

        if (application.Status == ApplicationStatus.Hired)
        {
            throw new ConflictException("Cannot withdraw an application that has already been accepted/hired.");
        }

        if (application.Status == ApplicationStatus.Rejected)
        {
            throw new ConflictException("Cannot withdraw an application that has been rejected.");
        }

        application.Status = ApplicationStatus.Withdrawn;
        application.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(application);
    }

    private static ApplicationResponseDto MapToDto(Application app)
    {
        return new ApplicationResponseDto
        {
            Id = app.Id,
            JobId = app.JobId,
            ApplicantName = app.ApplicantName,
            Email = app.Email,
            CoverLetter = app.CoverLetter,
            Status = app.Status,
            AppliedAtUtc = app.AppliedAtUtc,
            UpdatedAtUtc = app.UpdatedAtUtc
        };
    }
}
