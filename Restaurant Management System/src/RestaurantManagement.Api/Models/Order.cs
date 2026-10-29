using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Models;

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int? TableId { get; set; }
    public RestaurantTable? Table { get; set; }
    public OrderType OrderType { get; set; } = OrderType.DineIn;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<InventoryMovement> InventoryMovements { get; set; } = new List<InventoryMovement>();
}
