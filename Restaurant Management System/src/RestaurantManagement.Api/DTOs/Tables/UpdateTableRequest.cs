using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Tables;

public class UpdateTableRequest
{
    [Required]
    [Range(1, 9999, ErrorMessage = "Table number must be positive.")]
    public int TableNumber { get; set; }

    [Required]
    [Range(1, 100, ErrorMessage = "Capacity must be positive and reasonable (1 to 100).")]
    public int Capacity { get; set; }

    [Required]
    public bool IsActive { get; set; }
}
