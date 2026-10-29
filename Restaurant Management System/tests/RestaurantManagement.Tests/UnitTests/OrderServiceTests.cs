using RestaurantManagement.Api.DTOs.Orders;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class OrderServiceTests
{
    [Fact]
    public async Task CreateOrderAsync_CalculatesTotalsFromServerPrices()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var table = new RestaurantTable { TableNumber = 10, Capacity = 4, IsActive = true };
        var burger = new MenuItem { Name = "Burger", Category = "Main", Price = 10.00m, IsActive = true, IsAvailable = true };
        var fries = new MenuItem { Name = "Fries", Category = "Sides", Price = 4.50m, IsActive = true, IsAvailable = true };

        context.RestaurantTables.Add(table);
        context.MenuItems.AddRange(burger, fries);
        await context.SaveChangesAsync();

        var request = new CreateOrderRequest
        {
            TableId = table.Id,
            OrderType = OrderType.DineIn,
            Notes = "No onions please",
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = burger.Id, Quantity = 2 }, // 2 * 10 = 20
                new() { MenuItemId = fries.Id, Quantity = 3 }   // 3 * 4.5 = 13.5
            }
        };

        var order = await service.CreateOrderAsync(request);

        Assert.NotNull(order);
        Assert.Equal(33.50m, order.Subtotal);
        Assert.Equal(33.50m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
        Assert.Equal(2, order.Items.Count);

        // Verify snapshot values
        var burgerItem = order.Items.First(i => i.MenuItemId == burger.Id);
        Assert.Equal(10.00m, burgerItem.UnitPrice);
        Assert.Equal(20.00m, burgerItem.LineTotal);
    }

    [Fact]
    public async Task CreateOrderAsync_WhenMenuItemIsUnavailable_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var item = new MenuItem { Name = "Special Lobster", Category = "Special", Price = 45.00m, IsActive = true, IsAvailable = false };
        context.MenuItems.Add(item);
        await context.SaveChangesAsync();

        var request = new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = item.Id, Quantity = 1 }
            }
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateOrderAsync(request));
        Assert.Contains("unavailable", ex.Message);
    }

    [Fact]
    public async Task AddItemsToOrderAsync_WhenOrderIsPending_RecalculatesTotals()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var burger = new MenuItem { Name = "Burger", Category = "Main", Price = 10.00m, IsActive = true, IsAvailable = true };
        var drink = new MenuItem { Name = "Cola", Category = "Drinks", Price = 2.50m, IsActive = true, IsAvailable = true };
        context.MenuItems.AddRange(burger, drink);
        await context.SaveChangesAsync();

        var initialOrder = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = burger.Id, Quantity = 1 } }
        });

        Assert.Equal(10.00m, initialOrder.Total);

        var updated = await service.AddItemsToOrderAsync(initialOrder.Id, new AddOrderItemsRequest
        {
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = drink.Id, Quantity = 2 } }
        });

        Assert.Equal(15.00m, updated.Total);
        Assert.Equal(2, updated.Items.Count);
    }

    [Fact]
    public async Task AddItemsToOrderAsync_WhenOrderNotPending_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var burger = new MenuItem { Name = "Burger", Category = "Main", Price = 10.00m, IsActive = true, IsAvailable = true };
        context.MenuItems.Add(burger);
        await context.SaveChangesAsync();

        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = burger.Id, Quantity = 1 } }
        });

        // Move to Preparing
        await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Preparing });

        // Attempt to add items to Preparing order
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.AddItemsToOrderAsync(order.Id, new AddOrderItemsRequest
            {
                Items = new List<CreateOrderItemRequest> { new() { MenuItemId = burger.Id, Quantity = 1 } }
            }));
    }

    [Fact]
    public async Task UpdateOrderStatus_ToPreparing_DeductsInventoryExactlyOnce()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        // Setup inventory item: Beef Patty (10 in stock)
        var beef = new InventoryItem
        {
            Name = "Beef Patty",
            NormalizedName = "BEEF PATTY",
            Unit = "pcs",
            CurrentQuantity = 10m,
            ReorderThreshold = 2m,
            IsActive = true
        };
        // Setup inventory item: Burger Bun (20 in stock)
        var bun = new InventoryItem
        {
            Name = "Burger Bun",
            NormalizedName = "BURGER BUN",
            Unit = "pcs",
            CurrentQuantity = 20m,
            ReorderThreshold = 5m,
            IsActive = true
        };
        context.InventoryItems.AddRange(beef, bun);

        // Setup menu item with recipe: Double Cheeseburger requires 2 beef patties and 1 bun
        var burger = new MenuItem
        {
            Name = "Double Cheeseburger",
            Category = "Burgers",
            Price = 14.00m,
            IsActive = true,
            IsAvailable = true
        };
        context.MenuItems.Add(burger);
        await context.SaveChangesAsync();

        context.MenuItemIngredients.Add(new MenuItemIngredient
        {
            MenuItemId = burger.Id,
            InventoryItemId = beef.Id,
            QuantityRequired = 2m
        });
        context.MenuItemIngredients.Add(new MenuItemIngredient
        {
            MenuItemId = burger.Id,
            InventoryItemId = bun.Id,
            QuantityRequired = 1m
        });
        await context.SaveChangesAsync();

        // Create order for 3 Double Cheeseburgers
        // Required beef: 3 * 2 = 6 pcs (Remaining: 10 - 6 = 4)
        // Required buns: 3 * 1 = 3 pcs (Remaining: 20 - 3 = 17)
        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = burger.Id, Quantity = 3 }
            }
        });

        // Transition to Preparing
        var preparingOrder = await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Preparing
        });

        Assert.Equal(OrderStatus.Preparing, preparingOrder.Status);

        // Verify stock deducted
        var updatedBeef = await context.InventoryItems.FindAsync(beef.Id);
        var updatedBun = await context.InventoryItems.FindAsync(bun.Id);

        Assert.Equal(4m, updatedBeef!.CurrentQuantity);
        Assert.Equal(17m, updatedBun!.CurrentQuantity);

        // Verify audit movement records
        var movements = context.InventoryMovements.Where(m => m.OrderId == order.Id).ToList();
        Assert.Equal(2, movements.Count);
        Assert.All(movements, m => Assert.Equal(InventoryMovementType.OrderDeduction, m.MovementType));
        Assert.All(movements, m => Assert.True(m.QuantityChange < 0));

        // Test idempotency: repeated status call should not deduct again
        await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Preparing });

        var afterBeef = await context.InventoryItems.FindAsync(beef.Id);
        Assert.Equal(4m, afterBeef!.CurrentQuantity); // Still 4, not deducted again!
    }

    [Fact]
    public async Task UpdateOrderStatus_ToPreparing_WhenInsufficientStock_ThrowsConflictAndRollsBack()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var cheese = new InventoryItem
        {
            Name = "Cheese Slice",
            NormalizedName = "CHEESE SLICE",
            Unit = "pcs",
            CurrentQuantity = 1m, // Only 1 in stock!
            ReorderThreshold = 5m,
            IsActive = true
        };
        var pizza = new MenuItem
        {
            Name = "Cheese Pizza",
            Category = "Pizza",
            Price = 12.00m,
            IsActive = true,
            IsAvailable = true
        };
        context.InventoryItems.Add(cheese);
        context.MenuItems.Add(pizza);
        await context.SaveChangesAsync();

        context.MenuItemIngredients.Add(new MenuItemIngredient
        {
            MenuItemId = pizza.Id,
            InventoryItemId = cheese.Id,
            QuantityRequired = 4m // Needs 4 slices per pizza
        });
        await context.SaveChangesAsync();

        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = pizza.Id, Quantity = 1 } }
        });

        // Transitioning to Preparing should fail due to insufficient cheese
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Preparing }));

        Assert.Contains("Insufficient stock", ex.Message);

        // Verify cheese was NOT deducted
        var cheeseInDb = await context.InventoryItems.FindAsync(cheese.Id);
        Assert.Equal(1m, cheeseInDb!.CurrentQuantity);

        // Order remains in Pending
        var orderInDb = await context.Orders.FindAsync(order.Id);
        Assert.Equal(OrderStatus.Pending, orderInDb!.Status);
    }

    [Fact]
    public async Task UpdateOrderStatus_CancelledAfterPreparing_DoesNotAutoRestock()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var chicken = new InventoryItem
        {
            Name = "Chicken Breast",
            NormalizedName = "CHICKEN BREAST",
            Unit = "kg",
            CurrentQuantity = 10m,
            ReorderThreshold = 2m,
            IsActive = true
        };
        var chickenDish = new MenuItem
        {
            Name = "Grilled Chicken",
            Category = "Main",
            Price = 16.00m,
            IsActive = true,
            IsAvailable = true
        };
        context.InventoryItems.Add(chicken);
        context.MenuItems.Add(chickenDish);
        await context.SaveChangesAsync();

        context.MenuItemIngredients.Add(new MenuItemIngredient
        {
            MenuItemId = chickenDish.Id,
            InventoryItemId = chicken.Id,
            QuantityRequired = 0.5m
        });
        await context.SaveChangesAsync();

        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = chickenDish.Id, Quantity = 2 } } // 1.0kg needed
        });

        // Transition to Preparing (deducts 1.0kg)
        await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Preparing });
        var chickenAfterPrep = await context.InventoryItems.FindAsync(chicken.Id);
        Assert.Equal(9.0m, chickenAfterPrep!.CurrentQuantity);

        // Now cancel order after preparation has started
        await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest { Status = OrderStatus.Cancelled });

        // Stock should remain 9.0kg according to documented policy (already consumed during prep; no auto-restock)
        var chickenAfterCancel = await context.InventoryItems.FindAsync(chicken.Id);
        Assert.Equal(9.0m, chickenAfterCancel!.CurrentQuantity);
    }
}
