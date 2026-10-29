using System.Data;
using EventRegistration.Api.Data;
using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Registrations;
using EventRegistration.Api.Exceptions;
using EventRegistration.Api.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Api.Services;

public class RegistrationService : IRegistrationService
{
    private readonly EventDbContext _dbContext;

    public RegistrationService(EventDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RegistrationResponse> RegisterParticipantAsync(int eventId, CreateRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();

        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken)
                : null;

            Event? @event;

            if (_dbContext.Database.IsSqlServer())
            {
                // Acquire an update lock on the Event row to serialize concurrent registration capacity checks
                @event = await _dbContext.Events
                    .FromSqlInterpolated($"SELECT * FROM Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
                    .FirstOrDefaultAsync(cancellationToken);
            }
            else
            {
                @event = await _dbContext.Events
                    .FirstOrDefaultAsync(e => e.Id == eventId, cancellationToken);
            }

            if (@event == null)
            {
                throw new NotFoundException($"Event with ID {eventId} was not found.");
            }

            if (@event.StartDateTime <= DateTime.UtcNow)
            {
                throw new ConflictException("Cannot register for an event that has already started.");
            }

            var isAlreadyRegistered = await _dbContext.Registrations
                .AnyAsync(r => r.EventId == eventId && r.NormalizedEmail == normalizedEmail, cancellationToken);

            if (isAlreadyRegistered)
            {
                throw new ConflictException("Participant with this email is already registered for this event.");
            }

            var currentCount = await _dbContext.Registrations
                .CountAsync(r => r.EventId == eventId, cancellationToken);

            if (currentCount >= @event.Capacity)
            {
                throw new ConflictException("Event is at full capacity.");
            }

            var registration = new Registration
            {
                EventId = eventId,
                ParticipantName = request.ParticipantName.Trim(),
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                RegisteredAt = DateTime.UtcNow
            };

            _dbContext.Registrations.Add(registration);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }
            }
            catch (DbUpdateException ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }

                if (IsUniqueConstraintViolation(ex))
                {
                    throw new ConflictException("Participant with this email is already registered for this event.");
                }

                throw;
            }

            return new RegistrationResponse
            {
                Id = registration.Id,
                EventId = registration.EventId,
                ParticipantName = registration.ParticipantName,
                Email = registration.Email,
                RegisteredAt = registration.RegisteredAt,
                EventTitle = @event.Title
            };
        });
    }

    public async Task<PagedResult<RegistrationResponse>> GetEventRegistrationsAsync(int eventId, PaginationQuery query, CancellationToken cancellationToken = default)
    {
        var eventExists = await _dbContext.Events
            .AnyAsync(e => e.Id == eventId, cancellationToken);

        if (!eventExists)
        {
            throw new NotFoundException($"Event with ID {eventId} was not found.");
        }

        var baseQuery = _dbContext.Registrations
            .AsNoTracking()
            .Where(r => r.EventId == eventId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var items = await baseQuery
            .OrderBy(r => r.RegisteredAt)
            .ThenBy(r => r.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new RegistrationResponse
            {
                Id = r.Id,
                EventId = r.EventId,
                ParticipantName = r.ParticipantName,
                Email = r.Email,
                RegisteredAt = r.RegisteredAt,
                EventTitle = r.Event.Title
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<RegistrationResponse>(items, totalCount, query.PageNumber, query.PageSize);
    }

    public async Task<RegistrationResponse> GetRegistrationByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await _dbContext.Registrations
            .AsNoTracking()
            .Include(r => r.Event)
            .Where(r => r.Id == id)
            .Select(r => new RegistrationResponse
            {
                Id = r.Id,
                EventId = r.EventId,
                ParticipantName = r.ParticipantName,
                Email = r.Email,
                RegisteredAt = r.RegisteredAt,
                EventTitle = r.Event.Title
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (registration == null)
        {
            throw new NotFoundException($"Registration with ID {id} was not found.");
        }

        return registration;
    }

    public async Task CancelRegistrationAsync(int id, CancellationToken cancellationToken = default)
    {
        var registration = await _dbContext.Registrations
            .Include(r => r.Event)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (registration == null)
        {
            throw new NotFoundException($"Registration with ID {id} was not found.");
        }

        if (registration.Event.StartDateTime <= DateTime.UtcNow)
        {
            throw new ConflictException("Cannot cancel registration for an event that has already started.");
        }

        _dbContext.Registrations.Remove(registration);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException ex)
    {
        if (ex.InnerException is SqlException sqlEx)
        {
            // 2601: Cannot insert duplicate key row in object with unique index
            // 2627: Violation of %ls constraint '%.*ls'. Cannot insert duplicate key in object '%.*ls'
            return sqlEx.Number is 2601 or 2627;
        }

        var message = ex.InnerException?.Message ?? ex.Message;
        return message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("IX_Registrations_EventId_NormalizedEmail", StringComparison.OrdinalIgnoreCase);
    }
}
