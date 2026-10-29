using System.Net;
using System.Net.Http.Json;
using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Events;
using EventRegistration.Api.Dtos.Registrations;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.Tests.IntegrationTests;

public class RegistrationsApiTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegistrationsApiTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RegisterParticipant_WithValidData_Returns201Created()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Registration Flow Event",
            Location = "Hall 1",
            StartDateTime = DateTime.UtcNow.AddDays(15),
            EndDateTime = DateTime.UtcNow.AddDays(16),
            Capacity = 25
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        var regRequest = new CreateRegistrationRequest
        {
            ParticipantName = "Samuel Taylor",
            Email = "samuel.taylor@example.com"
        };

        var regRes = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", regRequest);

        Assert.Equal(HttpStatusCode.Created, regRes.StatusCode);
        Assert.NotNull(regRes.Headers.Location);

        var regContent = await regRes.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(regContent);
        Assert.True(regContent.Id > 0);
        Assert.Equal(createdEvent.Id, regContent.EventId);
        Assert.Equal("Samuel Taylor", regContent.ParticipantName);
        Assert.Equal("samuel.taylor@example.com", regContent.Email);
    }

    [Fact]
    public async Task RegisterParticipant_WithInvalidEmail_Returns400BadRequest()
    {
        var regRequest = new CreateRegistrationRequest
        {
            ParticipantName = "Bad Email User",
            Email = "not-an-email"
        };

        var response = await _client.PostAsJsonAsync("/api/events/1/registrations", regRequest);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RegisterParticipant_WhenEventDoesNotExist_Returns404NotFound()
    {
        var regRequest = new CreateRegistrationRequest
        {
            ParticipantName = "Ghost",
            Email = "ghost@example.com"
        };

        var response = await _client.PostAsJsonAsync("/api/events/999999/registrations", regRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("was not found", problem.Detail);
    }

    [Fact]
    public async Task RegisterParticipant_WhenDuplicateEmail_Returns409Conflict()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Duplicate Check Event",
            Location = "Room D",
            StartDateTime = DateTime.UtcNow.AddDays(20),
            EndDateTime = DateTime.UtcNow.AddDays(21),
            Capacity = 100
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        // First registration
        var first = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Duplicate Target",
            Email = "duplicate@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second registration with same email (mixed case)
        var second = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Duplicate Impostor",
            Email = "DUPLICATE@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("already registered", problem.Detail);
    }

    [Fact]
    public async Task RegisterParticipant_WhenEventIsFull_Returns409Conflict()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Capacity One Event",
            Location = "VIP Room",
            StartDateTime = DateTime.UtcNow.AddDays(10),
            EndDateTime = DateTime.UtcNow.AddDays(11),
            Capacity = 1
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        // First registration fills it
        var first = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "First",
            Email = "first.full@example.com"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Second registration exceeds capacity
        var second = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Second",
            Email = "second.full@example.com"
        });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("full capacity", problem.Detail);
    }

    [Fact]
    public async Task GetEventRegistrations_WhenExists_Returns200WithPagedItems()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Listing Registrations Event",
            Location = "Room 5",
            StartDateTime = DateTime.UtcNow.AddDays(18),
            EndDateTime = DateTime.UtcNow.AddDays(19),
            Capacity = 50
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        for (int i = 1; i <= 3; i++)
        {
            await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
            {
                ParticipantName = $"Attendee {i}",
                Email = $"attendee{i}@test.com"
            });
        }

        var response = await _client.GetAsync($"/api/events/{createdEvent.Id}/registrations?pageNumber=1&pageSize=10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var paged = await response.Content.ReadFromJsonAsync<PagedResult<RegistrationResponse>>();
        Assert.NotNull(paged);
        Assert.Equal(3, paged.TotalCount);
        Assert.Equal(3, paged.Items.Count);
    }

    [Fact]
    public async Task GetRegistrationById_WhenExists_Returns200Ok()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Fetch Reg Event",
            Location = "Lobby",
            StartDateTime = DateTime.UtcNow.AddDays(22),
            EndDateTime = DateTime.UtcNow.AddDays(23),
            Capacity = 10
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        var regRes = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Fetch Me",
            Email = "fetchme@test.com"
        });
        var createdReg = await regRes.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(createdReg);

        var response = await _client.GetAsync($"/api/registrations/{createdReg.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var fetched = await response.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(fetched);
        Assert.Equal(createdReg.Id, fetched.Id);
        Assert.Equal("Fetch Me", fetched.ParticipantName);
        Assert.Equal("fetchme@test.com", fetched.Email);
    }

    [Fact]
    public async Task GetRegistrationById_WhenNotFound_Returns404NotFound()
    {
        var response = await _client.GetAsync("/api/registrations/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("was not found", problem.Detail);
    }

    [Fact]
    public async Task CancelRegistration_BeforeEventStarts_Returns204NoContent()
    {
        var eventRes = await _client.PostAsJsonAsync("/api/events", new CreateEventRequest
        {
            Title = "Cancellable Event",
            Location = "Hall 2",
            StartDateTime = DateTime.UtcNow.AddDays(12),
            EndDateTime = DateTime.UtcNow.AddDays(13),
            Capacity = 20
        });
        var createdEvent = await eventRes.Content.ReadFromJsonAsync<EventResponse>();
        Assert.NotNull(createdEvent);

        var regRes = await _client.PostAsJsonAsync($"/api/events/{createdEvent.Id}/registrations", new CreateRegistrationRequest
        {
            ParticipantName = "Cancel Me",
            Email = "cancelme@test.com"
        });
        var createdReg = await regRes.Content.ReadFromJsonAsync<RegistrationResponse>();
        Assert.NotNull(createdReg);

        var cancelRes = await _client.DeleteAsync($"/api/registrations/{createdReg.Id}");

        Assert.Equal(HttpStatusCode.NoContent, cancelRes.StatusCode);

        // Verify registration is gone
        var getRes = await _client.GetAsync($"/api/registrations/{createdReg.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getRes.StatusCode);
    }

    [Fact]
    public async Task CancelRegistration_WhenNotFound_Returns404NotFound()
    {
        var cancelRes = await _client.DeleteAsync("/api/registrations/999999");

        Assert.Equal(HttpStatusCode.NotFound, cancelRes.StatusCode);
        var problem = await cancelRes.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("was not found", problem.Detail);
    }
}
