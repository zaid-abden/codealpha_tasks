using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Orders;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Services;

public class OrderService : IOrderService
{
    private readonly RestaurantDbContext _context;

    public OrderService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<OrderDto>> GetOrdersAsync(OrderFilterParams filter)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .AsQueryable();

        if (filter.FromDate.HasValue)
        {
            var fromUtc = filter.FromDate.Value.ToUniversalTime();
            query = query.Where(o => o.CreatedAtUtc >= fromUtc);
        }

        if (filter.ToDate.HasValue)
        {
            var toUtc = filter.ToDate.Value.ToUniversalTime();
            query = query.Where(o => o.CreatedAtUtc <= toUtc);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(o => o.Status == filter.Status.Value);
        }

        if (filter.OrderType.HasValue)
        {
            query = query.Where(o => o.OrderType == filter.OrderType.Value);
        }

        var totalCount = await query.CountAsync();

        var orders = await query
            .OrderByDescending(o => o.CreatedAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(o => MapToDto(o))
            .ToListAsync();

        return new PagedResult<OrderDto>(orders, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<OrderDto> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Table)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        return MapToDto(order);
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new BadRequestException("An order must contain at least one item.");
        }

        // Validate table for DineIn
        int? tableId = null;
        if (request.OrderType == OrderType.DineIn)
        {
            if (!request.TableId.HasValue)
            {
                throw new BadRequestException("A table ID is required for DineIn orders.");
            }

            var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == request.TableId.Value);
            if (table == null)
            {
                throw new NotFoundException($"Table with ID {request.TableId.Value} was not found.");
            }

            if (!table.IsActive)
            {
                throw new ConflictException($"Table {table.TableNumber} is currently inactive.");
            }

            tableId = table.Id;
        }

        // Fetch menu items and validate
        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var reqItem in request.Items)
        {
            if (reqItem.Quantity <= 0)
            {
                throw new BadRequestException($"Quantity for menu item ID {reqItem.MenuItemId} must be positive.");
            }

            if (!menuItems.TryGetValue(reqItem.MenuItemId, out var menuItem))
            {
                throw new NotFoundException($"Menu item with ID {reqItem.MenuItemId} was not found.");
            }

            if (!menuItem.IsActive)
            {
                throw new BadRequestException($"Menu item '{menuItem.Name}' is no longer active.");
            }

            if (!menuItem.IsAvailable)
            {
                throw new ConflictException($"Menu item '{menuItem.Name}' is currently unavailable.");
            }
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        }

        try
        {
            var now = DateTime.UtcNow;
            var orderNumber = $"ORD-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

            var order = new Order
            {
                OrderNumber = orderNumber,
                TableId = tableId,
                OrderType = request.OrderType,
                Status = OrderStatus.Pending,
                Notes = request.Notes?.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            decimal subtotal = 0;

            foreach (var reqItem in request.Items)
            {
                var menuItem = menuItems[reqItem.MenuItemId];
                var lineTotal = menuItem.Price * reqItem.Quantity;
                subtotal += lineTotal;

                order.Items.Add(new OrderItem
                {
                    MenuItemId = menuItem.Id,
                    ItemNameSnapshot = menuItem.Name,
                    UnitPrice = menuItem.Price,
                    Quantity = reqItem.Quantity,
                    LineTotal = lineTotal
                });
            }

            order.Subtotal = subtotal;
            order.Total = subtotal; // Can add tax or discount logic here if needed

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return await GetOrderByIdAsync(order.Id);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public async Task<OrderDto> AddItemsToOrderAsync(int id, AddOrderItemsRequest request)
    {
        if (request.Items == null || !request.Items.Any())
        {
            throw new BadRequestException("At least one item must be added.");
        }

        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        // Only Pending orders can have items added
        if (order.Status != OrderStatus.Pending)
        {
            throw new ConflictException(
                $"Cannot add items to order #{order.OrderNumber} because its status is '{order.Status}'. Items can only be added to Pending orders.");
        }

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _context.MenuItems
            .Where(m => menuItemIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var reqItem in request.Items)
        {
            if (reqItem.Quantity <= 0)
            {
                throw new BadRequestException($"Quantity for menu item ID {reqItem.MenuItemId} must be positive.");
            }

            if (!menuItems.TryGetValue(reqItem.MenuItemId, out var menuItem))
            {
                throw new NotFoundException($"Menu item with ID {reqItem.MenuItemId} was not found.");
            }

            if (!menuItem.IsActive)
            {
                throw new BadRequestException($"Menu item '{menuItem.Name}' is no longer active.");
            }

            if (!menuItem.IsAvailable)
            {
                throw new ConflictException($"Menu item '{menuItem.Name}' is currently unavailable.");
            }
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        }

        try
        {
            foreach (var reqItem in request.Items)
            {
                var menuItem = menuItems[reqItem.MenuItemId];
                var existingItem = order.Items.FirstOrDefault(oi => oi.MenuItemId == menuItem.Id);

                if (existingItem != null)
                {
                    existingItem.Quantity += reqItem.Quantity;
                    existingItem.LineTotal = existingItem.UnitPrice * existingItem.Quantity;
                }
                else
                {
                    var lineTotal = menuItem.Price * reqItem.Quantity;
                    order.Items.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        MenuItemId = menuItem.Id,
                        ItemNameSnapshot = menuItem.Name,
                        UnitPrice = menuItem.Price,
                        Quantity = reqItem.Quantity,
                        LineTotal = lineTotal
                    });
                }
            }

            order.Subtotal = order.Items.Sum(oi => oi.LineTotal);
            order.Total = order.Subtotal;
            order.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return await GetOrderByIdAsync(order.Id);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public async Task<OrderDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusRequest request)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            throw new NotFoundException($"Order with ID {id} was not found.");
        }

        var currentStatus = order.Status;
        var newStatus = request.Status;

        if (currentStatus == newStatus)
        {
            return await GetOrderByIdAsync(order.Id);
        }

        // Validate state transitions:
        // Pending -> Preparing, Cancelled
        // Preparing -> Ready, Cancelled
        // Ready -> Served, Cancelled
        // Served -> Completed, Cancelled
        // Completed -> None
        // Cancelled -> None
        bool isValidTransition = currentStatus switch
        {
            OrderStatus.Pending => newStatus is OrderStatus.Preparing or OrderStatus.Cancelled,
            OrderStatus.Preparing => newStatus is OrderStatus.Ready or OrderStatus.Cancelled,
            OrderStatus.Ready => newStatus is OrderStatus.Served or OrderStatus.Cancelled,
            OrderStatus.Served => newStatus is OrderStatus.Completed or OrderStatus.Cancelled,
            OrderStatus.Completed => false,
            OrderStatus.Cancelled => false,
            _ => false
        };

        if (!isValidTransition)
        {
            throw new ConflictException(
                $"Invalid order status transition from '{currentStatus}' to '{newStatus}'.");
        }

        // Handle inventory deduction when transitioning into Preparing
        if (newStatus == OrderStatus.Preparing)
        {
            await ProcessInventoryDeductionForOrderAsync(order);
        }

        order.Status = newStatus;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return await GetOrderByIdAsync(order.Id);
    }

    private async Task ProcessInventoryDeductionForOrderAsync(Order order)
    {
        // Check if deduction was already performed for this order (idempotency check)
        var alreadyDeducted = await _context.InventoryMovements
            .AnyAsync(m => m.OrderId == order.Id && m.MovementType == InventoryMovementType.OrderDeduction);

        if (alreadyDeducted)
        {
            return;
        }

        // Fetch ingredients for all ordered items
        var menuItemIds = order.Items.Select(oi => oi.MenuItemId).Distinct().ToList();
        var menuIngredients = await _context.MenuItemIngredients
            .Where(mi => menuItemIds.Contains(mi.MenuItemId))
            .ToListAsync();

        if (!menuIngredients.Any())
        {
            // No recipe configured for items in this order; no inventory to deduct (documented policy)
            return;
        }

        // Aggregate total required quantities per InventoryItemId
        var requiredQuantities = new Dictionary<int, decimal>();

        foreach (var orderItem in order.Items)
        {
            var ingredientsForItem = menuIngredients.Where(mi => mi.MenuItemId == orderItem.MenuItemId);
            foreach (var ing in ingredientsForItem)
            {
                var totalNeededForThisItem = ing.QuantityRequired * orderItem.Quantity;
                if (requiredQuantities.ContainsKey(ing.InventoryItemId))
                {
                    requiredQuantities[ing.InventoryItemId] += totalNeededForThisItem;
                }
                else
                {
                    requiredQuantities[ing.InventoryItemId] = totalNeededForThisItem;
                }
            }
        }

        var inventoryItemIds = requiredQuantities.Keys.ToList();

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            // Serializable or repeatable read to prevent race conditions during stock check and deduction
            transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        try
        {
            var inventoryItems = await _context.InventoryItems
                .Where(i => inventoryItemIds.Contains(i.Id))
                .ToListAsync();

            // 1. Verify all required inventory items exist and have sufficient stock
            foreach (var kvp in requiredQuantities)
            {
                var invItem = inventoryItems.FirstOrDefault(i => i.Id == kvp.Key);
                if (invItem == null)
                {
                    throw new ConflictException(
                        $"Inventory item ID {kvp.Key} required for order preparation does not exist.");
                }

                if (invItem.CurrentQuantity < kvp.Value)
                {
                    throw new ConflictException(
                        $"Insufficient stock for '{invItem.Name}'. Required: {kvp.Value:0.####} {invItem.Unit}, Available: {invItem.CurrentQuantity:0.####} {invItem.Unit}.");
                }
            }

            // 2. Perform atomic stock deductions and record movements
            var now = DateTime.UtcNow;
            foreach (var kvp in requiredQuantities)
            {
                var invItem = inventoryItems.First(i => i.Id == kvp.Key);
                var qtyToDeduct = kvp.Value;

                invItem.CurrentQuantity -= qtyToDeduct;
                invItem.UpdatedAtUtc = now;

                _context.InventoryMovements.Add(new InventoryMovement
                {
                    InventoryItemId = invItem.Id,
                    OrderId = order.Id,
                    QuantityChange = -qtyToDeduct, // Negative sign for deduction
                    MovementType = InventoryMovementType.OrderDeduction,
                    Reason = $"Automatic deduction for Order #{order.OrderNumber} preparation",
                    CreatedAtUtc = now
                });
            }

            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private static OrderDto MapToDto(Order order)
    {
        return new OrderDto
        {
            Id = order.Id,
            OrderNumber = order.OrderNumber,
            TableId = order.TableId,
            TableNumber = order.Table?.TableNumber,
            OrderType = order.OrderType,
            Status = order.Status,
            Notes = order.Notes,
            Subtotal = order.Subtotal,
            Total = order.Total,
            CreatedAtUtc = order.CreatedAtUtc,
            UpdatedAtUtc = order.UpdatedAtUtc,
            Items = order.Items.Select(oi => new OrderItemDto
            {
                Id = oi.Id,
                MenuItemId = oi.MenuItemId,
                ItemName = oi.ItemNameSnapshot,
                UnitPrice = oi.UnitPrice,
                Quantity = oi.Quantity,
                LineTotal = oi.LineTotal
            }).ToList()
        };
    }
}
