using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Events;

namespace EventRegistration.Api.Services;

public interface IEventService
{
    Task<PagedResult<EventResponse>> GetEventsAsync(EventQueryParameters parameters, CancellationToken cancellationToken = default);
    Task<EventResponse> GetEventByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<EventResponse> CreateEventAsync(CreateEventRequest request, CancellationToken cancellationToken = default);
    Task<EventResponse> UpdateEventAsync(int id, UpdateEventRequest request, CancellationToken cancellationToken = default);
    Task DeleteEventAsync(int id, CancellationToken cancellationToken = default);
}
