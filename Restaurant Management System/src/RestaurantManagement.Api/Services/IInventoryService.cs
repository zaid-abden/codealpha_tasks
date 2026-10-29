using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Inventory;
using RestaurantManagement.Api.DTOs.Reports;

namespace RestaurantManagement.Api.Services;

public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetInventoryItemsAsync(InventoryFilterParams filter);
    Task<InventoryItemDto> GetInventoryItemByIdAsync(int id);
    Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request);
    Task<InventoryItemDto> UpdateInventoryItemAsync(int id, UpdateInventoryItemRequest request);
    Task<InventoryItemDto> AdjustStockAsync(int id, InventoryAdjustmentRequest request);
    Task<List<LowStockReportDto>> GetLowStockItemsAsync();
    Task<List<InventoryMovementDto>> GetMovementsByItemIdAsync(int id);
}
