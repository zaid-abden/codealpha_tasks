using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Orders;

public class CreateOrderRequest : IValidatableObject
{
    public int? TableId { get; set; }

    [Required]
    public OrderType OrderType { get; set; } = OrderType.DineIn;

    [StringLength(500)]
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "An order must contain at least one item.")]
    public List<CreateOrderItemRequest> Items { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (OrderType == OrderType.DineIn && (!TableId.HasValue || TableId.Value <= 0))
        {
            yield return new ValidationResult(
                "A valid active TableId is required for DineIn orders.",
                new[] { nameof(TableId) });
        }
    }
}
