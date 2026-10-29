using System.Net;
using System.Net.Http.Json;
using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Events;
using EventRegistration.Api.Dtos.Registrations;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.Tests.IntegrationTests;

public class EventsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EventsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CreateEvent_WithValidData_Returns201Created()
    {
        var request = new CreateEventRequest
        {
            Title = "API Integration Workshop",
            Description = "Learning REST APIs",
            Location = "Hall 4",
            StartDateTime = DateTime.UtcNow.AddDays(20),
            EndDateTime = DateTime.UtcNow.AddDays(21),
            Capacity = 75
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var content = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(content);
        Assert.True(content.Id > 0);
        Assert.Equal("API Integration Workshop", content.Title);
        Assert.Equal(75, content.RemainingSpots);
    }

    [Fact]
    public async Task CreateEvent_WithEndBeforeStart_Returns400BadRequest()
    {
        var request = new CreateEventRequest
        {
            Title = "Invalid Event Dates",
            Location = "Virtual",
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(9),
            Capacity = 20
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithZeroCapacity_Returns400BadRequest()
    {
        var request = new CreateEventRequest
        {
            Title = "Invalid Capacity",
            Location = "Virtual",
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            Capacity = 0
        };

        var response = await _client.PostAsJsonAsync("/api/events", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetEventById_WhenExists_Returns200Ok()
    {
        // First create an event
        var createRequest = new CreateEventRequest
        {
            Title = "Fetch Event Test",
            Location = "Room 302",
            StartDateTime = DateTime.UtcNow.AddDays(12),
            EndDateTime = DateTime.UtcNow.AddDays(13),
            Capacity = 40
        };
        var createResponse = await _client.PostAsJsonAsync("/api/events", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);

        // Fetch it
        var response = await _client.GetAsync($"/api/events/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(created.Id, fetched.Id);
        Assert.Equal("Fetch Event Test", fetched.Title);
    }

    [Fact]
    public async Task GetEventById_WhenNotFound_Returns404NotFound()
    {
        var response = await _client.GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("was not found", problem.Detail);
    }

    [Fact]
    public async Task GetEvents_Returns200WithPaginatedResults()
    {
        var response = await _client.GetAsync("/api/events?pageNumber=1&pageSize=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<EventResponse>>();
        Assert.NotNull(paged);
        Assert.True(paged.TotalCount >= 0);
        Assert.Equal(1, paged.PageNumber);
        Assert.Equal(5, paged.PageSize);
    }

    [Fact]
    public async Task UpdateEvent_WhenExists_Returns200Ok()
    {
        var createRequest = new CreateEventRequest
        {
            Title = "Original Event Title",
            Location = "Room A",
            StartDateTime = DateTime.UtcNow.AddDays(5),
            EndDateTime = DateTime.UtcNow.AddDays(6),
            Capacity = 30
        };
        var createResponse = await _client.PostAsJsonAsync("/api/events", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(created);

        var updateRequest = new UpdateEventRequest
        {
            Title = "Updated Event Title",
            Location = "Room B",
            StartDateTime = created.StartDateTime,
            EndDateTime = created.EndDateTime,
            Capacity = 35
        };

        var response = await _client.PutAsJsonAsync($"/api/events/{created.Id}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(updated);
        Assert.Equal("Updated Event Title", updated.Title);
        Assert.Equal("Room B", updated.Location);
        Assert.Equal(35, updated.Capacity);
    }

    [Fact]
    public async Task UpdateEvent_WhenReducingCapacityBelowRegistrations_Returns409Conflict()
    {
        var createEventReq = new CreateEventRequest
        {
            Title = "Capacity Conflict Event",
            Location = "Room C",
            StartDateTime = DateTime.UtcNow.AddDays(8),
            EndDateTime = DateTime.UtcNow.AddDays(9),
            Capacity = 10
        };
        var eventRes = await _client.PostAsJsonAsync("/api/events", createEventReq);
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        // Register 2 participants
        await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "User 1",
            Email = "u1@test.com"
        });
        await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "User 2",
            Email = "u2@test.com"
        });

        // Try to update capacity to 1 (less than 2 registrations)
        var updateReq = new UpdateEventRequest
        {
            Title = createdEvent.Title,
            Location = createdEvent.Location,
            StartDateTime = createdEvent.StartDateTime,
            EndDateTime = createdEvent.EndDateTime,
            Capacity = 1
        };

        var updateRes = await _client.PutAsJsonAsync($"/api/events/{createdEvent.Id}", updateReq);

        Assert.Equal(HttpStatusCode.Conflict, updateRes.StatusCode);
        var problem = await updateRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Cannot reduce capacity", problem.Detail);
    }

    [Fact]
    public async Task DeleteEvent_WhenHasRegistrations_Returns409Conflict()
    {
        var createEventReq = new CreateEventRequest
        {
            Title = "Protected Event From Delete",
            Location = "Main Stage",
            StartDateTime = DateTime.UtcNow.AddDays(25),
            EndDateTime = DateTime.UtcNow.AddDays(26),
            Capacity = 50
        };
        var eventRes = await _client.PostAsJsonAsync("/api/events", createEventReq);
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        // Register participant
        await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Attendee",
            Email = "attendee@test.com"
        });

        // Attempt delete
        var deleteRes = await _client.DeleteAsync($"/api/events/{createdEvent.Id}");

        Assert.Equal(HttpStatusCode.Conflict, deleteRes.StatusCode);
        var problem = await deleteRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("active registrations", problem.Detail);
    }

    [Fact]
    public async Task DeleteEvent_WhenEmpty_Returns204NoContent()
    {
        var createEventReq = new CreateEventRequest
        {
            Title = "Unattended Event",
            Location = "Online",
            StartDateTime = DateTime.UtcNow.AddDays(30),
            EndDateTime = DateTime.UtcNow.AddDays(31),
            Capacity = 20
        };
        var eventRes = await _client.PostAsJsonAsync("/api/events", createEventReq);
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        var deleteRes = await _client.DeleteAsync($"/api/events/{createdEvent.Id}");

        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);

        // Verify it is gone
        var getRes = await _client.GetAsync($"/api/events/{createdEvent.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }
}
