using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Menu;

namespace RestaurantManagement.Api.Services;

public interface IMenuService
{
    Task<PagedResult<MenuItemDto>> GetMenuItemsAsync(MenuFilterParams filter);
    Task<MenuItemDto> GetMenuItemByIdAsync(int id);
    Task<MenuItemDto> CreateMenuItemAsync(CreateMenuItemRequest request);
    Task<MenuItemDto> UpdateMenuItemAsync(int id, UpdateMenuItemRequest request);
    Task<MenuItemDto> UpdateAvailabilityAsync(int id, bool isAvailable);
    Task DeleteMenuItemAsync(int id);
}
