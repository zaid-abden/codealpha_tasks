using EventRegistration.Api.Dtos.Common;
using EventRegistration.Api.Dtos.Registrations;

namespace EventRegistration.Api.Services;

public interface IRegistrationService
{
    Task<RegistrationResponse> RegisterParticipantAsync(int eventId, CreateRegistrationRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<RegistrationResponse>> GetEventRegistrationsAsync(int eventId, PaginationQuery query, CancellationToken cancellationToken = default);
    Task<RegistrationResponse> GetRegistrationByIdAsync(int id, CancellationToken cancellationToken = default);
    Task CancelRegistrationAsync(int id, CancellationToken cancellationToken = default);
}
