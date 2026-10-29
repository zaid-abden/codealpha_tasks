using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Models;

public class InventoryMovement
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public int? OrderId { get; set; }
    public Order? Order { get; set; }

    /// <summary>
    /// Sign convention: Negative values indicate deductions (e.g., -2.50 for order preparation);
    /// Positive values indicate additions/restocks (e.g., +10.00 for manual adjustment or replenishment).
    /// </summary>
    public decimal QuantityChange { get; set; }
    public InventoryMovementType MovementType { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
