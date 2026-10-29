using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Menu;

public class UpdateMenuItemAvailabilityRequest
{
    [Required]
    public bool IsAvailable { get; set; }
}
