using RestaurantManagement.Api.DTOs.Orders;
using RestaurantManagement.Api.DTOs.Reservations;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class ConcurrencyAndEdgeCaseTests
{
    [Fact]
    public async Task Reservation_PastStartTime_ThrowsBadRequestException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 401, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Time Traveler",
            Email = "past@test.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = DateTime.UtcNow.AddHours(-2), // In the past!
            DurationMinutes = 60
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateReservationAsync(request));
        Assert.Contains("future", ex.Message);
    }

    [Fact]
    public async Task Reservation_EndTimeBeforeStartTime_ThrowsBadRequestException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 402, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Invalid Order",
            Email = "invalid@test.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = DateTime.UtcNow.AddHours(4),
            EndAtUtc = DateTime.UtcNow.AddHours(2) // End before start!
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateReservationAsync(request));
        Assert.Contains("after start time", ex.Message);
    }

    [Fact]
    public async Task Reservation_TableCapacityTooSmall_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 403, Capacity = 2, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Big Party",
            Email = "party@test.com",
            PartySize = 6, // Exceeds table capacity 2
            TableId = table.Id,
            StartAtUtc = DateTime.UtcNow.AddHours(3),
            DurationMinutes = 90
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateReservationAsync(request));
        Assert.Contains("insufficient for party size", ex.Message);
    }

    [Fact]
    public async Task Reservation_InactiveTable_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 404, Capacity = 4, IsActive = false }; // Inactive
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Guest",
            Email = "guest@test.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = DateTime.UtcNow.AddHours(3),
            DurationMinutes = 90
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() => service.CreateReservationAsync(request));
        Assert.Contains("inactive", ex.Message);
    }

    [Fact]
    public async Task Order_DineInWithoutTable_ThrowsBadRequestException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        var item = new MenuItem { Name = "Soup", Category = "Soup", Price = 5.0m, IsActive = true, IsAvailable = true };
        context.MenuItems.Add(item);
        await context.SaveChangesAsync();

        var request = new CreateOrderRequest
        {
            OrderType = OrderType.DineIn,
            TableId = null, // Missing for DineIn!
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = item.Id, Quantity = 1 } }
        };

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => service.CreateOrderAsync(request));
        Assert.Contains("table ID is required", ex.Message);
    }

    [Fact]
    public async Task Order_WithoutRecipe_DeductsZeroInventoryAndSucceeds()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new OrderService(context);

        // Menu item with no recipe ingredients
        var cannedSoda = new MenuItem
        {
            Name = "Canned Soda",
            Category = "Drinks",
            Price = 3.00m,
            IsActive = true,
            IsAvailable = true
        };
        context.MenuItems.Add(cannedSoda);
        await context.SaveChangesAsync();

        var order = await service.CreateOrderAsync(new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest> { new() { MenuItemId = cannedSoda.Id, Quantity = 2 } }
        });

        // Moving to Preparing should succeed even with zero recipe ingredients configured
        var updated = await service.UpdateOrderStatusAsync(order.Id, new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Preparing
        });

        Assert.Equal(OrderStatus.Preparing, updated.Status);
        Assert.Empty(context.InventoryMovements.Where(m => m.OrderId == order.Id));
    }

    [Fact]
    public async Task ConcurrentReservationRequests_OnlyOneWins()
    {
        // Test running 10 concurrent reservation attempts for the same single slot on table 500
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 500, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var start = DateTime.UtcNow.AddDays(4).Date.AddHours(19);
        var end = start.AddHours(2);

        var tasks = Enumerable.Range(1, 10).Select(async i =>
        {
            try
            {
                var req = new CreateReservationRequest
                {
                    CustomerName = $"Guest {i}",
                    Email = $"guest{i}@example.com",
                    PartySize = 2,
                    TableId = table.Id,
                    StartAtUtc = start,
                    EndAtUtc = end
                };
                return await service.CreateReservationAsync(req);
            }
            catch (ConflictException)
            {
                return null;
            }
        });

        var results = await Task.WhenAll(tasks);
        var successfulReservations = results.Where(r => r != null).ToList();

        // Exactly one reservation must win the slot
        Assert.Single(successfulReservations);
    }
}
