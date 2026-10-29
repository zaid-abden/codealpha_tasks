using RestaurantManagement.Api.DTOs.Menu;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class MenuServiceTests
{
    [Fact]
    public async Task CreateMenuItemAsync_WithValidData_ReturnsCreatedMenuItem()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new MenuService(context);

        var request = new CreateMenuItemRequest
        {
            Name = "Classic Cheeseburger",
            Description = "Juicy beef patty with aged cheddar",
            Category = "Burgers",
            Price = 12.99m,
            IsAvailable = true
        };

        var result = await service.CreateMenuItemAsync(request);

        Assert.NotNull(result);
        Assert.True(result.Id > 0);
        Assert.Equal("Classic Cheeseburger", result.Name);
        Assert.Equal(12.99m, result.Price);
        Assert.True(result.IsAvailable);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task GetMenuItemByIdAsync_WhenItemDoesNotExist_ThrowsNotFoundException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new MenuService(context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetMenuItemByIdAsync(9999));
    }

    [Fact]
    public async Task UpdateAvailabilityAsync_TogglesAvailability()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new MenuService(context);

        var created = await service.CreateMenuItemAsync(new CreateMenuItemRequest
        {
            Name = "Caesar Salad",
            Category = "Salads",
            Price = 8.50m,
            IsAvailable = true
        });

        var updated = await service.UpdateAvailabilityAsync(created.Id, false);
        Assert.False(updated.IsAvailable);

        var refreshed = await service.GetMenuItemByIdAsync(created.Id);
        Assert.False(refreshed.IsAvailable);
    }

    [Fact]
    public async Task DeleteMenuItemAsync_WhenReferencedByHistoricalOrders_SoftDeactivatesItem()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new MenuService(context);

        var menuItem = new MenuItem
        {
            Name = "Pepperoni Pizza",
            Category = "Pizza",
            Price = 15.00m,
            IsActive = true,
            IsAvailable = true
        };
        context.MenuItems.Add(menuItem);
        await context.SaveChangesAsync();

        // Simulate historical order item referencing this menu item
        var order = new Order
        {
            OrderNumber = "ORD-TEST-001",
            Subtotal = 15.00m,
            Total = 15.00m
        };
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var orderItem = new OrderItem
        {
            OrderId = order.Id,
            MenuItemId = menuItem.Id,
            ItemNameSnapshot = menuItem.Name,
            UnitPrice = menuItem.Price,
            Quantity = 1,
            LineTotal = 15.00m
        };
        context.OrderItems.Add(orderItem);
        await context.SaveChangesAsync();

        // Delete menu item
        await service.DeleteMenuItemAsync(menuItem.Id);

        // Verify soft deactivation in database
        var itemInDb = await context.MenuItems.FindAsync(menuItem.Id);
        Assert.NotNull(itemInDb);
        Assert.False(itemInDb.IsActive);
        Assert.False(itemInDb.IsAvailable);

        // Historical order still retains order item snapshot
        var historicalOrderItem = await context.OrderItems.FindAsync(orderItem.Id);
        Assert.NotNull(historicalOrderItem);
        Assert.Equal("Pepperoni Pizza", historicalOrderItem.ItemNameSnapshot);
        Assert.Equal(15.00m, historicalOrderItem.UnitPrice);
    }

    [Fact]
    public async Task GetMenuItemsAsync_WithCategoryAndKeywordFilter_FiltersCorrectly()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new MenuService(context);

        await service.CreateMenuItemAsync(new CreateMenuItemRequest { Name = "Margherita Pizza", Category = "Pizza", Price = 11.00m });
        await service.CreateMenuItemAsync(new CreateMenuItemRequest { Name = "BBQ Chicken Pizza", Category = "Pizza", Price = 14.50m });
        await service.CreateMenuItemAsync(new CreateMenuItemRequest { Name = "Beef Burger", Category = "Burgers", Price = 13.00m });

        var filter = new MenuFilterParams
        {
            Category = "Pizza",
            Keyword = "BBQ"
        };

        var result = await service.GetMenuItemsAsync(filter);

        Assert.Single(result.Items);
        Assert.Equal("BBQ Chicken Pizza", result.Items[0].Name);
    }
}
