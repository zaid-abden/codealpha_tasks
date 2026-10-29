using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Inventory;

public class CreateInventoryItemRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(30, MinimumLength = 1)]
    public string Unit { get; set; } = string.Empty;

    [Required]
    [Range(0.0, 1000000.0, ErrorMessage = "Initial quantity must be non-negative.")]
    public decimal CurrentQuantity { get; set; }

    [Required]
    [Range(0.0, 1000000.0, ErrorMessage = "Reorder threshold must be non-negative.")]
    public decimal ReorderThreshold { get; set; }
}
