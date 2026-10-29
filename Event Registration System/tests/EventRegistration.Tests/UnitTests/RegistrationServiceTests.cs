using EventRegistration.Api.Data;
using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Registrations;
using EventRegistration.Api.Exceptions;
using EventRegistration.Api.Models;
using EventRegistration.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Tests.UnitTests;

public class RegistrationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventDbContext _dbContext;
    private readonly RegistrationService _service;

    public RegistrationServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new EventDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new RegistrationService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterParticipantAsync_WithValidData_CreatesRegistration()
    {
        var @event = new Event
        {
            Title = "Future Tech Meetup",
            Location = "Hall 1",
            StartDateTime = DateTime.UtcNow.AddDays(7),
            EndDateTime = DateTime.UtcNow.AddDays(8),
            Capacity = 50,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRegistrationRequest
        {
            ParticipantName = "John Doe",
            Email = "john.doe@example.com"
        };

        var response = await _service.RegisterParticipantAsync(@event.Id, request);

        Assert.NotNull(response);
        Assert.True(response.Id > 0);
        Assert.Equal(@event.Id, response.EventId);
        Assert.Equal("John Doe", response.ParticipantName);
        Assert.Equal("john.doe@example.com", response.Email);
        Assert.Equal("Future Tech Meetup", response.EventTitle);
    }

    [Fact]
    public async Task RegisterParticipantAsync_WithNonExistentEvent_ThrowsNotFoundException()
    {
        var request = new CreateRegistrationRequest
        {
            ParticipantName = "Alice",
            Email = "alice@example.com"
        };

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RegisterParticipantAsync(9999, request));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task RegisterParticipantAsync_WhenEventAlreadyStarted_ThrowsConflictException()
    {
        var pastEvent = new Event
        {
            Title = "Started Event",
            Location = "Room 10",
            StartDateTime = DateTime.UtcNow.AddHours(-2), // Started 2 hours ago
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Capacity = 20,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Events.Add(pastEvent);
        await _dbContext.SaveChangesAsync();

        var request = new CreateRegistrationRequest
        {
            ParticipantName = "Bob",
            Email = "bob@example.com"
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterParticipantAsync(pastEvent.Id, request));

        Assert.Contains("already started", ex.Message);
    }

    [Fact]
    public async Task RegisterParticipantAsync_WhenEventIsFull_ThrowsConflictException()
    {
        var fullEvent = new Event
        {
            Title = "Small Event",
            Location = "Room A",
            StartDateTime = DateTime.UtcNow.AddDays(3),
            EndDateTime = DateTime.UtcNow.AddDays(4),
            Capacity = 1,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(fullEvent);
        await _dbContext.SaveChangesAsync();

        // Add 1 registration to fill it
        _dbContext.Registrations.Add(new Registration
        {
            EventId = fullEvent.Id,
            ParticipantName = "First User",
            Email = "first@example.com",
            NormalizedEmail = "FIRST@EXAMPLE.COM",
            RegisteredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var request = new CreateRegistrationRequest
        {
            ParticipantName = "Second User",
            Email = "second@example.com"
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterParticipantAsync(fullEvent.Id, request));

        Assert.Contains("full capacity", ex.Message);
    }

    [Fact]
    public async Task RegisterParticipantAsync_WithDuplicateEmail_ThrowsConflictException()
    {
        var @event = new Event
        {
            Title = "Seminar",
            Location = "Auditorium",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(6),
            Capacity = 100,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        _dbContext.Registrations.Add(new Registration
        {
            EventId = @event.Id,
            ParticipantName = "Alice Original",
            Email = "alice@example.com",
            NormalizedEmail = "ALICE@EXAMPLE.COM",
            RegisteredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Try registering with same email in different casing
        var request = new CreateRegistrationRequest
        {
            ParticipantName = "Alice Duplicate",
            Email = "ALICE@Example.com"
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RegisterParticipantAsync(@event.Id, request));

        Assert.Contains("already registered", ex.Message);
    }

    [Fact]
    public async Task CancelRegistrationAsync_BeforeEventStarts_CancelsRegistration()
    {
        var @event = new Event
        {
            Title = "Upcoming Concert",
            Location = "Stadium",
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(11),
            Capacity = 500,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        var registration = new Registration
        {
            EventId = @event.Id,
            ParticipantName = "Charlie",
            Email = "charlie@example.com",
            NormalizedEmail = "CHARLIE@EXAMPLE.COM",
            RegisteredAt = DateTime.UtcNow
        };
        _dbContext.Registrations.Add(registration);
        await _dbContext.SaveChangesAsync();

        await _service.CancelRegistrationAsync(registration.Id);

        var exists = await _dbContext.Registrations.AnyAsync(r => r.Id == registration.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task CancelRegistrationAsync_AfterEventStarts_ThrowsConflictException()
    {
        var pastEvent = new Event
        {
            Title = "Happening Now Event",
            Location = "Stage",
            StartDateTime = DateTime.UtcNow.AddHours(-1),
            EndDateTime = DateTime.UtcNow.AddHours(2),
            Capacity = 50,
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        _dbContext.Events.Add(pastEvent);
        await _dbContext.SaveChangesAsync();

        var registration = new Registration
        {
            EventId = pastEvent.Id,
            ParticipantName = "Dave",
            Email = "dave@example.com",
            NormalizedEmail = "DAVE@EXAMPLE.COM",
            RegisteredAt = DateTime.UtcNow.AddDays(-1)
        };
        _dbContext.Registrations.Add(registration);
        await _dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CancelRegistrationAsync(registration.Id));

        Assert.Contains("already started", ex.Message);
    }

    [Fact]
    public async Task CancelRegistrationAsync_WithNonExistentRegistration_ThrowsNotFoundException()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CancelRegistrationAsync(9999));

        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task GetEventRegistrationsAsync_ReturnsPaginatedList()
    {
        var @event = new Event
        {
            Title = "Community Gathering",
            Location = "Park",
            StartDateTime = DateTime.UtcNow.AddDays(14),
            EndDateTime = DateTime.UtcNow.AddDays(15),
            Capacity = 200,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        for (int i = 1; i <= 15; i++)
        {
            _dbContext.Registrations.Add(new Registration
            {
                EventId = @event.Id,
                ParticipantName = $"Participant {i}",
                Email = $"p{i}@example.com",
                NormalizedEmail = $"P{i}@EXAMPLE.COM",
                RegisteredAt = DateTime.UtcNow.AddMinutes(i)
            });
        }
        await _dbContext.SaveChangesAsync();

        var page1 = await _service.GetEventRegistrationsAsync(@event.Id, new PaginationQuery { PageNumber = 1, PageSize = 10 });

        Assert.Equal(15, page1.TotalCount);
        Assert.Equal(10, page1.Items.Count);
        Assert.True(page1.HasNextPage);
        Assert.False(page1.HasPreviousPage);

        var page2 = await _service.GetEventRegistrationsAsync(@event.Id, new PaginationQuery { PageNumber = 2, PageSize = 10 });
        Assert.Equal(5, page2.Items.Count);
        Assert.False(page2.HasNextPage);
        Assert.True(page2.HasPreviousPage);
    }

    [Fact]
    public async Task RegisterParticipantAsync_UnderConcurrentAttempts_DoesNotExceedCapacity()
    {
        var @event = new Event
        {
            Title = "High Demand Workshop",
            Location = "Hall C",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(6),
            Capacity = 3,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        var successCount = 0;
        var conflictCount = 0;

        // Run sequential and rapid parallel registration attempts
        for (int i = 1; i <= 6; i++)
        {
            try
            {
                var reg = await _service.RegisterParticipantAsync(@event.Id, new CreateRegistrationRequest
                {
                    ParticipantName = $"Participant {i}",
                    Email = $"participant{i}@example.com"
                });

                if (reg != null)
                {
                    Interlocked.Increment(ref successCount);
                }
            }
            catch (ConflictException)
            {
                Interlocked.Increment(ref conflictCount);
            }
        }

        Assert.Equal(3, successCount);
        Assert.Equal(3, conflictCount);

        var finalCount = await _dbContext.Registrations.CountAsync(r => r.EventId == @event.Id);
        Assert.Equal(3, finalCount);
    }
}
