using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Inventory;

public class InventoryAdjustmentRequest
{
    [Required]
    [Range(-1000000.0, 1000000.0, ErrorMessage = "Quantity change must be within realistic adjustment bounds.")]
    public decimal QuantityChange { get; set; }

    [Required]
    [StringLength(250, MinimumLength = 1, ErrorMessage = "A reason for the stock adjustment is required.")]
    public string Reason { get; set; } = string.Empty;
}
