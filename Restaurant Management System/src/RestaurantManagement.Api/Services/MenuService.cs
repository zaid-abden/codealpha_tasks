using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Menu;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;

namespace RestaurantManagement.Api.Services;

public class MenuService : IMenuService
{
    private readonly RestaurantDbContext _context;

    public MenuService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<MenuItemDto>> GetMenuItemsAsync(MenuFilterParams filter)
    {
        var query = _context.MenuItems
            .AsNoTracking()
            .Include(m => m.Ingredients)
                .ThenInclude(i => i.InventoryItem)
            .Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            var cat = filter.Category.Trim();
            query = query.Where(m => m.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(m => m.Name.Contains(keyword) || (m.Description != null && m.Description.Contains(keyword)));
        }

        if (filter.IsAvailable.HasValue)
        {
            query = query.Where(m => m.IsAvailable == filter.IsAvailable.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(m => m.Category)
            .ThenBy(m => m.Name)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(m => MapToDto(m))
            .ToListAsync();

        return new PagedResult<MenuItemDto>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<MenuItemDto> GetMenuItemByIdAsync(int id)
    {
        var item = await _context.MenuItems
            .AsNoTracking()
            .Include(m => m.Ingredients)
                .ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (item == null)
        {
            throw new NotFoundException($"Menu item with ID {id} was not found or is inactive.");
        }

        return MapToDto(item);
    }

    public async Task<MenuItemDto> CreateMenuItemAsync(CreateMenuItemRequest request)
    {
        var menuItem = new MenuItem
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            Category = request.Category.Trim(),
            Price = request.Price,
            IsAvailable = request.IsAvailable,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        if (request.Ingredients != null && request.Ingredients.Any())
        {
            var ingredientItemIds = request.Ingredients.Select(i => i.InventoryItemId).Distinct().ToList();
            var existingInventoryCount = await _context.InventoryItems
                .CountAsync(i => ingredientItemIds.Contains(i.Id) && i.IsActive);

            if (existingInventoryCount != ingredientItemIds.Count)
            {
                throw new BadRequestException("One or more ingredient inventory items do not exist or are inactive.");
            }

            foreach (var ing in request.Ingredients)
            {
                menuItem.Ingredients.Add(new MenuItemIngredient
                {
                    InventoryItemId = ing.InventoryItemId,
                    QuantityRequired = ing.QuantityRequired
                });
            }
        }

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();

        return await GetMenuItemByIdAsync(menuItem.Id);
    }

    public async Task<MenuItemDto> UpdateMenuItemAsync(int id, UpdateMenuItemRequest request)
    {
        var menuItem = await _context.MenuItems
            .Include(m => m.Ingredients)
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (menuItem == null)
        {
            throw new NotFoundException($"Menu item with ID {id} was not found.");
        }

        menuItem.Name = request.Name.Trim();
        menuItem.Description = request.Description?.Trim();
        menuItem.Category = request.Category.Trim();
        menuItem.Price = request.Price;
        menuItem.IsAvailable = request.IsAvailable;
        menuItem.UpdatedAtUtc = DateTime.UtcNow;

        if (request.Ingredients != null)
        {
            var ingredientItemIds = request.Ingredients.Select(i => i.InventoryItemId).Distinct().ToList();
            var existingInventoryCount = await _context.InventoryItems
                .CountAsync(i => ingredientItemIds.Contains(i.Id) && i.IsActive);

            if (existingInventoryCount != ingredientItemIds.Count)
            {
                throw new BadRequestException("One or more ingredient inventory items do not exist or are inactive.");
            }

            _context.MenuItemIngredients.RemoveRange(menuItem.Ingredients);
            menuItem.Ingredients.Clear();

            foreach (var ing in request.Ingredients)
            {
                menuItem.Ingredients.Add(new MenuItemIngredient
                {
                    MenuItemId = menuItem.Id,
                    InventoryItemId = ing.InventoryItemId,
                    QuantityRequired = ing.QuantityRequired
                });
            }
        }

        await _context.SaveChangesAsync();

        return await GetMenuItemByIdAsync(menuItem.Id);
    }

    public async Task<MenuItemDto> UpdateAvailabilityAsync(int id, bool isAvailable)
    {
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.Id == id && m.IsActive);

        if (menuItem == null)
        {
            throw new NotFoundException($"Menu item with ID {id} was not found.");
        }

        menuItem.IsAvailable = isAvailable;
        menuItem.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetMenuItemByIdAsync(menuItem.Id);
    }

    public async Task DeleteMenuItemAsync(int id)
    {
        var menuItem = await _context.MenuItems
            .FirstOrDefaultAsync(m => m.Id == id);

        if (menuItem == null)
        {
            throw new NotFoundException($"Menu item with ID {id} was not found.");
        }

        var isReferencedInOrders = await _context.OrderItems.AnyAsync(oi => oi.MenuItemId == id);

        if (isReferencedInOrders)
        {
            // Soft deactivation to preserve historical orders and prices
            menuItem.IsActive = false;
            menuItem.IsAvailable = false;
            menuItem.UpdatedAtUtc = DateTime.UtcNow;
        }
        else
        {
            // Can be deleted or soft deactivated
            _context.MenuItems.Remove(menuItem);
        }

        await _context.SaveChangesAsync();
    }

    private static MenuItemDto MapToDto(MenuItem item)
    {
        return new MenuItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Category = item.Category,
            Price = item.Price,
            IsAvailable = item.IsAvailable,
            IsActive = item.IsActive,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc,
            Ingredients = item.Ingredients.Select(i => new MenuItemIngredientDto
            {
                Id = i.Id,
                InventoryItemId = i.InventoryItemId,
                InventoryItemName = i.InventoryItem?.Name ?? string.Empty,
                Unit = i.InventoryItem?.Unit ?? string.Empty,
                QuantityRequired = i.QuantityRequired
            }).ToList()
        };
    }
}
