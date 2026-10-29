using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Reservations;

public class ReservationFilterParams : PaginationParams
{
    public DateTime? Date { get; set; }
    public ReservationStatus? Status { get; set; }
    public int? TableId { get; set; }
}
