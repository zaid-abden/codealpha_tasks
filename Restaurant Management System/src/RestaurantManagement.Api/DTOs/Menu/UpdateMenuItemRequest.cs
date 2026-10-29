using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Menu;

public class UpdateMenuItemRequest
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

    public bool IsAvailable { get; set; }

    public List<SetMenuItemIngredientRequest>? Ingredients { get; set; }
}
