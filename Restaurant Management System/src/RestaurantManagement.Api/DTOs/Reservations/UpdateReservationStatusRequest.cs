using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Reservations;

public class UpdateReservationStatusRequest
{
    [Required]
    public ReservationStatus Status { get; set; }
}
