using RestaurantManagement.Api.DTOs.Common;

namespace RestaurantManagement.Api.DTOs.Inventory;

public class InventoryFilterParams : PaginationParams
{
    public string? Keyword { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? IsActive { get; set; }
}
