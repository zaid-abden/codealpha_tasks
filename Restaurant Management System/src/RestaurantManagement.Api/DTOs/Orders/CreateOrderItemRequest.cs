using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Orders;

public class CreateOrderItemRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "MenuItemId must be a valid positive integer.")]
    public int MenuItemId { get; set; }

    [Required]
    [Range(1, 1000, ErrorMessage = "Quantity must be a positive integer between 1 and 1000.")]
    public int Quantity { get; set; }
}
