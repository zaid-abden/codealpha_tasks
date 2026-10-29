using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Events;
using EventRegistration.Api.Dtos.Registrations;
using EventRegistration.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;
    private readonly IRegistrationService _registrationService;

    public EventsController(IEventService eventService, IRegistrationService registrationService)
    {
        _eventService = eventService;
        _registrationService = registrationService;
    }

    /// <summary>
    /// Returns a paginated list of events, with optional search and upcoming filter.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<EventResponse>>> GetEvents(
        [FromQuery] EventQueryParameters parameters,
        CancellationToken cancellationToken)
    {
        var result = await _eventService.GetEventsAsync(parameters, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the details of a single event by ID.
    /// </summary>
    [HttpGet("{id:int}", Name = nameof(GetEventById))]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEventById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await _eventService.GetEventByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new event.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponse>> CreateEvent(
        [FromBody] CreateEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _eventService.CreateEventAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetEventById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing event.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(
        [FromRoute] int id,
        [FromBody] UpdateEventRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _eventService.UpdateEventAsync(id, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes an event only if it has no active registrations.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEvent(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        await _eventService.DeleteEventAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Registers a participant for an event.
    /// </summary>
    [HttpPost("{eventId:int}/registrations")]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegistrationResponse>> RegisterParticipant(
        [FromRoute] int eventId,
        [FromBody] CreateRegistrationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registrationService.RegisterParticipantAsync(eventId, request, cancellationToken);
        return CreatedAtAction(
            nameof(RegistrationsController.GetRegistrationById),
            "Registrations",
            new { id = result.Id },
            result);
    }

    /// <summary>
    /// Returns paginated registrations for a specified event.
    /// </summary>
    [HttpGet("{eventId:int}/registrations")]
    [ProducesResponseType(typeof(PagedResult<RegistrationResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<RegistrationResponse>>> GetEventRegistrations(
        [FromRoute] int eventId,
        [FromQuery] PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _registrationService.GetEventRegistrationsAsync(eventId, query, cancellationToken);
        return Ok(result);
    }
}
