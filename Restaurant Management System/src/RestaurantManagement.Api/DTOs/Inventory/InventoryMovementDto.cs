using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Inventory;

public class InventoryMovementDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public int? OrderId { get; set; }
    public decimal QuantityChange { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
