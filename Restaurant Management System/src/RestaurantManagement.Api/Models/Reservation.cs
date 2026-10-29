using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Models;

public class Reservation
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public int PartySize { get; set; }
    public int TableId { get; set; }
    public RestaurantTable Table { get; set; } = null!;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
