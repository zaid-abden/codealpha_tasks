namespace RestaurantManagement.Api.DTOs.Orders;

public class OrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
