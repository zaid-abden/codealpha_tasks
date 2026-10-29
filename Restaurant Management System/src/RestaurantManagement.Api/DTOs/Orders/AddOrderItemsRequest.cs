using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Orders;

public class AddOrderItemsRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "At least one item must be added.")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();
}
