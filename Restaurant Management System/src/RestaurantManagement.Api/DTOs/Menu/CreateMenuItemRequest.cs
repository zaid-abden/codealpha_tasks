using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Menu;

public class CreateMenuItemRequest
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    [Required]
    [StringLength(60, MinimumLength = 1)]
    public string Category { get; set; } = string.Empty;

    [Range(0.00, 100000.00, ErrorMessage = "Price must be non-negative.")]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public List<SetMenuItemIngredientRequest>? Ingredients { get; set; }
}

public class SetMenuItemIngredientRequest
{
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "A valid InventoryItemId is required.")]
    public int InventoryItemId { get; set; }

    [Required]
    [Range(0.0001, 100000.0000, ErrorMessage = "Quantity required must be strictly positive.")]
    public decimal QuantityRequired { get; set; }
}
