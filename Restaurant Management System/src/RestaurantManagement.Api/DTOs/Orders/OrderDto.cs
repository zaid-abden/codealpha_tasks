using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Orders;

public class OrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public int? TableId { get; set; }
    public int? TableNumber { get; set; }
    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; }
    public string? Notes { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public List<OrderItemDto> Items { get; set; } = new();
}
