using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Reservations;

public class ReservationDto
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int PartySize { get; set; }
    public int TableId { get; set; }
    public int TableNumber { get; set; }
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public ReservationStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
