using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Inventory;
using RestaurantManagement.Api.DTOs.Reports;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Services;

public class InventoryService : IInventoryService
{
    private readonly RestaurantDbContext _context;

    public InventoryService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<InventoryItemDto>> GetInventoryItemsAsync(InventoryFilterParams filter)
    {
        var query = _context.InventoryItems.AsNoTracking().AsQueryable();

        if (filter.IsActive.HasValue)
        {
            query = query.Where(i => i.IsActive == filter.IsActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var keyword = filter.Keyword.Trim();
            query = query.Where(i => i.Name.Contains(keyword) || i.Unit.Contains(keyword));
        }

        if (filter.LowStockOnly.HasValue && filter.LowStockOnly.Value)
        {
            query = query.Where(i => i.CurrentQuantity <= i.ReorderThreshold);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(i => i.Name)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(i => MapToDto(i))
            .ToListAsync();

        return new PagedResult<InventoryItemDto>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<InventoryItemDto> GetInventoryItemByIdAsync(int id)
    {
        var item = await _context.InventoryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            throw new NotFoundException($"Inventory item with ID {id} was not found.");
        }

        return MapToDto(item);
    }

    public async Task<InventoryItemDto> CreateInventoryItemAsync(CreateInventoryItemRequest request)
    {
        var normalized = request.Name.Trim().ToUpperInvariant();
        var exists = await _context.InventoryItems.AnyAsync(i => i.NormalizedName == normalized);

        if (exists)
        {
            throw new ConflictException($"An inventory item named '{request.Name.Trim()}' already exists.");
        }

        var item = new InventoryItem
        {
            Name = request.Name.Trim(),
            NormalizedName = normalized,
            Unit = request.Unit.Trim(),
            CurrentQuantity = request.CurrentQuantity,
            ReorderThreshold = request.ReorderThreshold,
            IsActive = true,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryItems.Add(item);

        if (request.CurrentQuantity > 0)
        {
            _context.InventoryMovements.Add(new InventoryMovement
            {
                InventoryItem = item,
                QuantityChange = request.CurrentQuantity,
                MovementType = InventoryMovementType.InitialStock,
                Reason = "Initial inventory stock setup",
                CreatedAtUtc = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();

        return MapToDto(item);
    }

    public async Task<InventoryItemDto> UpdateInventoryItemAsync(int id, UpdateInventoryItemRequest request)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            throw new NotFoundException($"Inventory item with ID {id} was not found.");
        }

        var normalized = request.Name.Trim().ToUpperInvariant();
        var duplicate = await _context.InventoryItems
            .AnyAsync(i => i.NormalizedName == normalized && i.Id != id);

        if (duplicate)
        {
            throw new ConflictException($"Another inventory item named '{request.Name.Trim()}' already exists.");
        }

        item.Name = request.Name.Trim();
        item.NormalizedName = normalized;
        item.Unit = request.Unit.Trim();
        item.ReorderThreshold = request.ReorderThreshold;
        item.IsActive = request.IsActive;
        item.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return MapToDto(item);
    }

    public async Task<InventoryItemDto> AdjustStockAsync(int id, InventoryAdjustmentRequest request)
    {
        var item = await _context.InventoryItems.FirstOrDefaultAsync(i => i.Id == id);

        if (item == null)
        {
            throw new NotFoundException($"Inventory item with ID {id} was not found.");
        }

        var newQuantity = item.CurrentQuantity + request.QuantityChange;
        if (newQuantity < 0)
        {
            throw new ConflictException(
                $"Adjustment of {request.QuantityChange:0.####} {item.Unit} would result in negative stock. Current stock: {item.CurrentQuantity:0.####} {item.Unit}.");
        }

        item.CurrentQuantity = newQuantity;
        item.UpdatedAtUtc = DateTime.UtcNow;

        var movement = new InventoryMovement
        {
            InventoryItemId = item.Id,
            QuantityChange = request.QuantityChange,
            MovementType = request.QuantityChange >= 0 ? InventoryMovementType.Restock : InventoryMovementType.ManualAdjustment,
            Reason = request.Reason.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.InventoryMovements.Add(movement);
        await _context.SaveChangesAsync();

        return MapToDto(item);
    }

    public async Task<List<LowStockReportDto>> GetLowStockItemsAsync()
    {
        return await _context.InventoryItems
            .AsNoTracking()
            .Where(i => i.IsActive && i.CurrentQuantity <= i.ReorderThreshold)
            .OrderBy(i => i.CurrentQuantity / (i.ReorderThreshold == 0 ? 1 : i.ReorderThreshold))
            .ThenBy(i => i.Name)
            .Select(i => new LowStockReportDto
            {
                InventoryItemId = i.Id,
                Name = i.Name,
                Unit = i.Unit,
                CurrentQuantity = i.CurrentQuantity,
                ReorderThreshold = i.ReorderThreshold
            })
            .ToListAsync();
    }

    public async Task<List<InventoryMovementDto>> GetMovementsByItemIdAsync(int id)
    {
        var itemExists = await _context.InventoryItems.AnyAsync(i => i.Id == id);
        if (!itemExists)
        {
            throw new NotFoundException($"Inventory item with ID {id} was not found.");
        }

        return await _context.InventoryMovements
            .AsNoTracking()
            .Where(m => m.InventoryItemId == id)
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new InventoryMovementDto
            {
                Id = m.Id,
                InventoryItemId = m.InventoryItemId,
                OrderId = m.OrderId,
                QuantityChange = m.QuantityChange,
                MovementType = m.MovementType,
                Reason = m.Reason,
                CreatedAtUtc = m.CreatedAtUtc
            })
            .ToListAsync();
    }

    private static InventoryItemDto MapToDto(InventoryItem item)
    {
        return new InventoryItemDto
        {
            Id = item.Id,
            Name = item.Name,
            NormalizedName = item.NormalizedName,
            Unit = item.Unit,
            CurrentQuantity = item.CurrentQuantity,
            ReorderThreshold = item.ReorderThreshold,
            IsActive = item.IsActive,
            UpdatedAtUtc = item.UpdatedAtUtc
        };
    }
}
