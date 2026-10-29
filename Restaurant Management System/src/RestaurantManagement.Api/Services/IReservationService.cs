using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Reservations;

namespace RestaurantManagement.Api.Services;

public interface IReservationService
{
    Task<PagedResult<ReservationDto>> GetReservationsAsync(ReservationFilterParams filter);
    Task<ReservationDto> GetReservationByIdAsync(int id);
    Task<ReservationDto> CreateReservationAsync(CreateReservationRequest request);
    Task<ReservationDto> UpdateStatusAsync(int id, UpdateReservationStatusRequest request);
    Task<ReservationDto> CancelReservationAsync(int id);
}
