using EventRegistration.Api.Data;
using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Events;
using EventRegistration.Api.Exceptions;
using EventRegistration.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Api.Services;

public class EventService : IEventService
{
    private readonly EventDbContext _dbContext;

    public EventService(EventDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedResult<EventResponse>> GetEventsAsync(EventQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        IQueryable<Event> query = _dbContext.Events.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(parameters.Search))
        {
            var search = parameters.Search.Trim();
            query = query.Where(e => e.Title.Contains(search));
        }

        if (parameters.UpcomingOnly == true)
        {
            var nowUtc = DateTime.UtcNow;
            query = query.Where(e => e.StartDateTime > nowUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.StartDateTime)
            .ThenBy(e => e.Id)
            .Skip((parameters.PageNumber - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .Select(e => new EventResponse
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Location = e.Location,
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime,
                Capacity = e.Capacity,
                RegisteredCount = e.Registrations.Count(),
                RemainingSpots = e.Capacity - e.Registrations.Count() < 0 ? 0 : e.Capacity - e.Registrations.Count(),
                CreatedAt = e.CreatedAt
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<EventResponse>(items, totalCount, parameters.PageNumber, parameters.PageSize);
    }

    public async Task<EventResponse> GetEventByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var response = await _dbContext.Events
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new EventResponse
            {
                Id = e.Id,
                Title = e.Title,
                Description = e.Description,
                Location = e.Location,
                StartDateTime = e.StartDateTime,
                EndDateTime = e.EndDateTime,
                Capacity = e.Capacity,
                RegisteredCount = e.Registrations.Count(),
                RemainingSpots = e.Capacity - e.Registrations.Count() < 0 ? 0 : e.Capacity - e.Registrations.Count(),
                CreatedAt = e.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (response == null)
        {
            throw new NotFoundException($"Event with ID {id} was not found.");
        }

        return response;
    }

    public async Task<EventResponse> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken = default)
    {
        var startUtc = NormalizeToUtc(request.StartDateTime);
        var endUtc = NormalizeToUtc(request.EndDateTime);

        if (endUtc <= startUtc)
        {
            throw new ValidationException("End date/time must be strictly after start date/time.");
        }

        if (request.Capacity <= 0)
        {
            throw new ValidationException("Capacity must be greater than 0.");
        }

        var newEvent = new Event
        {
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Location = request.Location.Trim(),
            StartDateTime = startUtc,
            EndDateTime = endUtc,
            Capacity = request.Capacity,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Events.Add(newEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EventResponse
        {
            Id = newEvent.Id,
            Title = newEvent.Title,
            Description = newEvent.Description,
            Location = newEvent.Location,
            StartDateTime = newEvent.StartDateTime,
            EndDateTime = newEvent.EndDateTime,
            Capacity = newEvent.Capacity,
            RegisteredCount = 0,
            RemainingSpots = newEvent.Capacity,
            CreatedAt = newEvent.CreatedAt
        };
    }

    public async Task<EventResponse> UpdateEventAsync(int id, UpdateEventRequest request, CancellationToken cancellationToken = default)
    {
        var @event = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (@event == null)
        {
            throw new NotFoundException($"Event with ID {id} was not found.");
        }

        var startUtc = NormalizeToUtc(request.StartDateTime);
        var endUtc = NormalizeToUtc(request.EndDateTime);

        if (endUtc <= startUtc)
        {
            throw new ValidationException("End date/time must be strictly after start date/time.");
        }

        if (request.Capacity <= 0)
        {
            throw new ValidationException("Capacity must be greater than 0.");
        }

        var currentRegistrations = await _dbContext.Registrations
            .CountAsync(r => r.EventId == id, cancellationToken);

        if (request.Capacity < currentRegistrations)
        {
            throw new ConflictException(
                $"Cannot reduce capacity to {request.Capacity} because there are already {currentRegistrations} active registrations.");
        }

        @event.Title = request.Title.Trim();
        @event.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        @event.Location = request.Location.Trim();
        @event.StartDateTime = startUtc;
        @event.EndDateTime = endUtc;
        @event.Capacity = request.Capacity;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new EventResponse
        {
            Id = @event.Id,
            Title = @event.Title,
            Description = @event.Description,
            Location = @event.Location,
            StartDateTime = @event.StartDateTime,
            EndDateTime = @event.EndDateTime,
            Capacity = @event.Capacity,
            RegisteredCount = currentRegistrations,
            RemainingSpots = Math.Max(0, @event.Capacity - currentRegistrations),
            CreatedAt = @event.CreatedAt
        };
    }

    public async Task DeleteEventAsync(int id, CancellationToken cancellationToken = default)
    {
        var @event = await _dbContext.Events
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

        if (@event == null)
        {
            throw new NotFoundException($"Event with ID {id} was not found.");
        }

        var hasRegistrations = await _dbContext.Registrations
            .AnyAsync(r => r.EventId == id, cancellationToken);

        if (hasRegistrations)
        {
            throw new ConflictException(
                "Cannot delete event because it has active registrations. Events with registrations cannot be deleted to preserve registration integrity.");
        }

        _dbContext.Events.Remove(@event);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DateTime NormalizeToUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }
}
