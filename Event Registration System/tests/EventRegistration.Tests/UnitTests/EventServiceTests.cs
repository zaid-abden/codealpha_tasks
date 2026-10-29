using EventRegistration.Api.Data;
using EventRegistration.Api.Dtos.Events;
using EventRegistration.Api.Exceptions;
using EventRegistration.Api.Models;
using EventRegistration.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace EventRegistration.Tests.UnitTests;

public class EventServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly EventDbContext _dbContext;
    private readonly EventService _service;

    public EventServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<EventDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new EventDbContext(options);
        _dbContext.Database.EnsureCreated();

        _service = new EventService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateEventAsync_WithValidData_ReturnsCreatedEvent()
    {
        var request = new CreateEventRequest
        {
            Title = "Tech Summit 2026",
            Description = "Annual developer conference",
            Location = "Convention Hall A",
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(11),
            Capacity = 100
        };

        var response = await _service.CreateEventAsync(request);

        Assert.NotNull(response);
        Assert.True(response.Id > 0);
        Assert.Equal("Tech Summit 2026", response.Title);
        Assert.Equal("Convention Hall A", response.Location);
        Assert.Equal(100, response.Capacity);
        Assert.Equal(0, response.RegisteredCount);
        Assert.Equal(100, response.RemainingSpots);
    }

    [Fact]
    public async Task CreateEventAsync_WithEndBeforeStart_ThrowsValidationException()
    {
        var request = new CreateEventRequest
        {
            Title = "Invalid Dates Event",
            Location = "Online",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(4), // End is before Start!
            Capacity = 50
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateEventAsync(request));
        Assert.Contains("strictly after", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateEventAsync_WithNonPositiveCapacity_ThrowsValidationException(int invalidCapacity)
    {
        var request = new CreateEventRequest
        {
            Title = "Zero Capacity Event",
            Location = "Room 101",
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            Capacity = invalidCapacity
        };

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.CreateEventAsync(request));
        Assert.Contains("greater than 0", ex.Message);
    }

    [Fact]
    public async Task GetEventByIdAsync_WithNonExistentId_ThrowsNotFoundException()
    {
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetEventByIdAsync(9999));
        Assert.Contains("was not found", ex.Message);
    }

    [Fact]
    public async Task UpdateEventAsync_WithCapacityBelowRegistrationCount_ThrowsConflictException()
    {
        var @event = new Event
        {
            Title = "Full Workshop",
            Location = "Lab B",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(6),
            Capacity = 10,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        // Add 5 registrations
        for (int i = 1; i <= 5; i++)
        {
            _dbContext.Registrations.Add(new Registration
            {
                EventId = @event.Id,
                ParticipantName = $"User {i}",
                Email = $"user{i}@example.com",
                NormalizedEmail = $"USER{i}@EXAMPLE.COM",
                RegisteredAt = DateTime.UtcNow
            });
        }
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Title = "Full Workshop Renamed",
            Location = "Lab B",
            StartDateTime = @event.StartDateTime,
            EndDateTime = @event.EndDateTime,
            Capacity = 4 // Trying to reduce capacity below 5 registrations!
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateEventAsync(@event.Id, updateRequest));
        Assert.Contains("Cannot reduce capacity", ex.Message);
    }

    [Fact]
    public async Task UpdateEventAsync_WithValidNewCapacity_UpdatesEventSuccessfully()
    {
        var @event = new Event
        {
            Title = "Workshop",
            Location = "Lab A",
            StartDateTime = DateTime.UtcNow.AddDays(3),
            EndDateTime = DateTime.UtcNow.AddDays(4),
            Capacity = 20,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        var updateRequest = new UpdateEventRequest
        {
            Title = "Workshop Updated",
            Location = "Lab C",
            StartDateTime = @event.StartDateTime,
            EndDateTime = @event.EndDateTime,
            Capacity = 50
        };

        var response = await _service.UpdateEventAsync(@event.Id, updateRequest);

        Assert.Equal("Workshop Updated", response.Title);
        Assert.Equal("Lab C", response.Location);
        Assert.Equal(50, response.Capacity);
    }

    [Fact]
    public async Task DeleteEventAsync_WithExistingRegistrations_ThrowsConflictException()
    {
        var @event = new Event
        {
            Title = "Protected Event",
            Location = "Auditorium",
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(11),
            Capacity = 30,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        _dbContext.Registrations.Add(new Registration
        {
            EventId = @event.Id,
            ParticipantName = "Jane Doe",
            Email = "jane@example.com",
            NormalizedEmail = "JANE@EXAMPLE.COM",
            RegisteredAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteEventAsync(@event.Id));
        Assert.Contains("active registrations", ex.Message);
    }

    [Fact]
    public async Task DeleteEventAsync_WithoutRegistrations_DeletesSuccessfully()
    {
        var @event = new Event
        {
            Title = "Empty Event",
            Location = "Online",
            StartDateTime = DateTime.UtcNow.AddDays(2),
            EndDateTime = DateTime.UtcNow.AddDays(3),
            Capacity = 50,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Events.Add(@event);
        await _dbContext.SaveChangesAsync();

        await _service.DeleteEventAsync(@event.Id);

        var exists = await _dbContext.Events.AnyAsync(e => e.Id == @event.Id);
        Assert.False(exists);
    }

    [Fact]
    public async Task GetEventsAsync_WithSearchAndUpcomingFilter_FiltersCorrectly()
    {
        var pastEvent = new Event
        {
            Title = "Old Conference",
            Location = "Room 1",
            StartDateTime = DateTime.UtcNow.AddDays(-10),
            EndDateTime = DateTime.UtcNow.AddDays(-9),
            Capacity = 20,
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        var futureEvent1 = new Event
        {
            Title = "AI Summit Future",
            Location = "Room 2",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(6),
            Capacity = 50,
            CreatedAt = DateTime.UtcNow
        };
        var futureEvent2 = new Event
        {
            Title = "Cloud Expo Future",
            Location = "Room 3",
            StartDateTime = DateTime.UtcNow.AddDays(15),
            EndDateTime = DateTime.UtcNow.AddDays(16),
            Capacity = 100,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Events.AddRange(pastEvent, futureEvent1, futureEvent2);
        await _dbContext.SaveChangesAsync();

        var upcomingResult = await _service.GetEventsAsync(new EventQueryParameters
        {
            UpcomingOnly = true
        });

        Assert.Equal(2, upcomingResult.TotalCount);
        Assert.DoesNotContain(upcomingResult.Items, e => e.Title == "Old Conference");

        var searchResult = await _service.GetEventsAsync(new EventQueryParameters
        {
            Search = "AI Summit"
        });

        Assert.Single(searchResult.Items);
        Assert.Equal("AI Summit Future", searchResult.Items[0].Title);
    }
}
