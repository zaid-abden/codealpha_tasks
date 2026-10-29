using JobBoard.Api.Data;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Exceptions;
using JobBoard.Api.Models;
using JobBoard.Api.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobBoard.Api.Services;

public class JobService : IJobService
{
    private readonly JobBoardDbContext _context;

    public JobService(JobBoardDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<JobResponseDto>> GetPublishedJobsAsync(JobFilterDto filter, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = _context.Jobs
            .AsNoTracking()
            .Where(j => j.Status == JobStatus.Published &&
                        (!j.ApplicationDeadlineUtc.HasValue || j.ApplicationDeadlineUtc.Value > now));

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchLower = filter.Search.Trim().ToLower();
            query = query.Where(j =>
                j.Title.ToLower().Contains(searchLower) ||
                j.Description.ToLower().Contains(searchLower) ||
                j.CompanyName.ToLower().Contains(searchLower));
        }

        if (!string.IsNullOrWhiteSpace(filter.Location))
        {
            var locationLower = filter.Location.Trim().ToLower();
            query = query.Where(j => j.Location.ToLower().Contains(locationLower));
        }

        if (filter.EmploymentType.HasValue)
        {
            query = query.Where(j => j.EmploymentType == filter.EmploymentType.Value);
        }

        if (filter.WorkArrangement.HasValue)
        {
            query = query.Where(j => j.WorkArrangement == filter.WorkArrangement.Value);
        }

        if (filter.MinSalary.HasValue)
        {
            query = query.Where(j =>
                (j.MaximumSalary.HasValue && j.MaximumSalary.Value >= filter.MinSalary.Value) ||
                (j.MinimumSalary.HasValue && j.MinimumSalary.Value >= filter.MinSalary.Value));
        }

        if (filter.MaxSalary.HasValue)
        {
            query = query.Where(j =>
                (j.MinimumSalary.HasValue && j.MinimumSalary.Value <= filter.MaxSalary.Value) ||
                (j.MaximumSalary.HasValue && j.MaximumSalary.Value <= filter.MaxSalary.Value));
        }

        query = filter.SortBy?.ToLowerInvariant() switch
        {
            "salary_asc" => query.OrderBy(j => j.MinimumSalary ?? j.MaximumSalary ?? 0),
            "salary_desc" => query.OrderByDescending(j => j.MaximumSalary ?? j.MinimumSalary ?? 0),
            "title" => query.OrderBy(j => j.Title),
            _ => query.OrderByDescending(j => j.CreatedAtUtc)
        };

        var totalCount = await query.CountAsync(cancellationToken);

        var pageNumber = filter.PageNumber < 1 ? 1 : filter.PageNumber;
        var pageSize = filter.PageSize < 1 ? 10 : (filter.PageSize > 50 ? 50 : filter.PageSize);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(j => MapToDto(j))
            .ToListAsync(cancellationToken);

        return new PagedResultDto<JobResponseDto>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<JobResponseDto> GetJobByIdAsync(int id, bool publicOnly = true, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new NotFoundException($"Job with ID {id} was not found.");
        }

        if (publicOnly)
        {
            var isPublic = job.Status == JobStatus.Published &&
                           (!job.ApplicationDeadlineUtc.HasValue || job.ApplicationDeadlineUtc.Value > DateTime.UtcNow);

            if (!isPublic)
            {
                throw new NotFoundException($"Job with ID {id} was not found or is not publicly available.");
            }
        }

        return MapToDto(job);
    }

    public async Task<JobResponseDto> CreateJobAsync(JobCreateDto dto, CancellationToken cancellationToken = default)
    {
        ValidateSalary(dto.MinimumSalary, dto.MaximumSalary);

        DateTime? deadlineUtc = null;
        if (dto.ApplicationDeadlineUtc.HasValue)
        {
            deadlineUtc = DateTime.SpecifyKind(dto.ApplicationDeadlineUtc.Value, DateTimeKind.Utc);
            if (deadlineUtc.Value <= DateTime.UtcNow)
            {
                throw new ValidationException("Application deadline must be a future date and time in UTC.");
            }
        }

        var now = DateTime.UtcNow;

        var job = new Job
        {
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            CompanyName = dto.CompanyName.Trim(),
            Location = dto.Location.Trim(),
            EmploymentType = dto.EmploymentType,
            WorkArrangement = dto.WorkArrangement,
            MinimumSalary = dto.MinimumSalary,
            MaximumSalary = dto.MaximumSalary,
            CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "EGP" : dto.CurrencyCode.Trim().ToUpperInvariant(),
            Status = JobStatus.Draft, // Explicitly Draft by default
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ApplicationDeadlineUtc = deadlineUtc
        };

        _context.Jobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(job);
    }

    public async Task<JobResponseDto> UpdateJobAsync(int id, JobUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job == null)
        {
            throw new NotFoundException($"Job with ID {id} was not found.");
        }

        if (job.Status == JobStatus.Closed || job.Status == JobStatus.Archived)
        {
            throw new ConflictException($"Cannot update a job that is in '{job.Status}' status.");
        }

        ValidateSalary(dto.MinimumSalary, dto.MaximumSalary);

        DateTime? deadlineUtc = null;
        if (dto.ApplicationDeadlineUtc.HasValue)
        {
            deadlineUtc = DateTime.SpecifyKind(dto.ApplicationDeadlineUtc.Value, DateTimeKind.Utc);
            if (deadlineUtc.Value <= DateTime.UtcNow && job.Status == JobStatus.Published)
            {
                throw new ValidationException("Application deadline for a published job must be in the future.");
            }
        }

        job.Title = dto.Title.Trim();
        job.Description = dto.Description.Trim();
        job.CompanyName = dto.CompanyName.Trim();
        job.Location = dto.Location.Trim();
        job.EmploymentType = dto.EmploymentType;
        job.WorkArrangement = dto.WorkArrangement;
        job.MinimumSalary = dto.MinimumSalary;
        job.MaximumSalary = dto.MaximumSalary;
        job.CurrencyCode = string.IsNullOrWhiteSpace(dto.CurrencyCode) ? "EGP" : dto.CurrencyCode.Trim().ToUpperInvariant();
        job.ApplicationDeadlineUtc = deadlineUtc;
        job.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(job);
    }

    public async Task DeleteJobAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs
            .Include(j => j.Applications)
            .FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

        if (job == null)
        {
            throw new NotFoundException($"Job with ID {id} was not found.");
        }

        // If the job already has applications, soft-delete to Archived status to preserve history
        if (job.Applications.Any())
        {
            job.Status = JobStatus.Archived;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            _context.Jobs.Remove(job);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<JobResponseDto> PublishJobAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job == null)
        {
            throw new NotFoundException($"Job with ID {id} was not found.");
        }

        if (job.Status != JobStatus.Draft)
        {
            throw new ConflictException($"Only draft jobs can be published. Current status is '{job.Status}'.");
        }

        if (string.IsNullOrWhiteSpace(job.Title) ||
            string.IsNullOrWhiteSpace(job.Description) ||
            string.IsNullOrWhiteSpace(job.CompanyName) ||
            string.IsNullOrWhiteSpace(job.Location))
        {
            throw new ConflictException("Job cannot be published because required fields are incomplete.");
        }

        if (job.ApplicationDeadlineUtc.HasValue && job.ApplicationDeadlineUtc.Value <= DateTime.UtcNow)
        {
            throw new ConflictException("Cannot publish a job with an expired application deadline.");
        }

        job.Status = JobStatus.Published;
        job.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(job);
    }

    public async Task<JobResponseDto> CloseJobAsync(int id, CancellationToken cancellationToken = default)
    {
        var job = await _context.Jobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        if (job == null)
        {
            throw new NotFoundException($"Job with ID {id} was not found.");
        }

        if (job.Status != JobStatus.Published)
        {
            throw new ConflictException($"Only published jobs can be closed. Current status is '{job.Status}'.");
        }

        job.Status = JobStatus.Closed;
        job.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToDto(job);
    }

    private static void ValidateSalary(decimal? min, decimal? max)
    {
        if (min.HasValue && min.Value < 0)
        {
            throw new ValidationException("Minimum salary must be non-negative.");
        }

        if (max.HasValue && max.Value < 0)
        {
            throw new ValidationException("Maximum salary must be non-negative.");
        }

        if (min.HasValue && max.HasValue && max.Value < min.Value)
        {
            throw new ValidationException("Maximum salary must be greater than or equal to minimum salary.");
        }
    }

    private static JobResponseDto MapToDto(Job job)
    {
        return new JobResponseDto
        {
            Id = job.Id,
            Title = job.Title,
            Description = job.Description,
            CompanyName = job.CompanyName,
            Location = job.Location,
            EmploymentType = job.EmploymentType,
            WorkArrangement = job.WorkArrangement,
            MinimumSalary = job.MinimumSalary,
            MaximumSalary = job.MaximumSalary,
            CurrencyCode = job.CurrencyCode,
            Status = job.Status,
            CreatedAtUtc = job.CreatedAtUtc,
            UpdatedAtUtc = job.UpdatedAtUtc,
            ApplicationDeadlineUtc = job.ApplicationDeadlineUtc
        };
    }
}
