using RestaurantManagement.Api.DTOs.Common;

namespace RestaurantManagement.Api.DTOs.Menu;

public class MenuFilterParams : PaginationParams
{
    public string? Category { get; set; }
    public string? Keyword { get; set; }
    public bool? IsAvailable { get; set; }
}
