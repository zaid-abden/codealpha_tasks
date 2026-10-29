namespace RestaurantManagement.Api.DTOs.Inventory;

public class InventoryItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public decimal ReorderThreshold { get; set; }
    public bool IsActive { get; set; }
    public bool IsLowStock => CurrentQuantity <= ReorderThreshold;
    public DateTime UpdatedAtUtc { get; set; }
}
