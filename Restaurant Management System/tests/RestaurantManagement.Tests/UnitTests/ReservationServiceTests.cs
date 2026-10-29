using RestaurantManagement.Api.DTOs.Reservations;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class ReservationServiceTests
{
    [Fact]
    public async Task CreateReservationAsync_WithValidDataAndExplicitTable_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 1, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var start = DateTime.UtcNow.AddHours(2);
        var request = new CreateReservationRequest
        {
            CustomerName = "Alice Smith",
            Email = "alice@example.com",
            PartySize = 4,
            TableId = table.Id,
            StartAtUtc = start,
            DurationMinutes = 90
        };

        var result = await service.CreateReservationAsync(request);

        Assert.NotNull(result);
        Assert.Equal(table.Id, result.TableId);
        Assert.Equal(ReservationStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task CreateReservationAsync_WhenOverlappingInterval_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 2, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddDays(1);
        // Existing reservation: 14:00 to 16:00
        await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "First Guest",
            Email = "guest1@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(14),
            EndAtUtc = baseTime.Date.AddHours(16)
        });

        // Overlapping request: 15:00 to 17:00
        var overlappingRequest = new CreateReservationRequest
        {
            CustomerName = "Second Guest",
            Email = "guest2@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(15),
            EndAtUtc = baseTime.Date.AddHours(17)
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReservationAsync(overlappingRequest));

        Assert.Contains("already reserved", ex.Message);
    }

    [Fact]
    public async Task CreateReservationAsync_AdjacentIntervals_DoNotConflict()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 3, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddDays(1);
        // Reservation 1: 12:00 to 14:00
        var res1 = await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "Slot 1",
            Email = "slot1@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(12),
            EndAtUtc = baseTime.Date.AddHours(14)
        });

        // Adjacent Reservation 2: 14:00 to 16:00 (half-open interval, starts exactly when previous ends)
        var res2 = await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "Slot 2",
            Email = "slot2@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(14),
            EndAtUtc = baseTime.Date.AddHours(16)
        });

        Assert.NotNull(res1);
        Assert.NotNull(res2);
        Assert.Equal(table.Id, res1.TableId);
        Assert.Equal(table.Id, res2.TableId);
    }

    [Fact]
    public async Task CreateReservationAsync_CancelledReservation_DoesNotBlockAvailability()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 4, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var baseTime = DateTime.UtcNow.AddDays(2);
        var initial = await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "Cancelled Guest",
            Email = "cancel@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(18),
            EndAtUtc = baseTime.Date.AddHours(20)
        });

        // Cancel it
        await service.CancelReservationAsync(initial.Id);

        // Booking the same slot should now succeed
        var replacement = await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "New Guest",
            Email = "new@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = baseTime.Date.AddHours(18),
            EndAtUtc = baseTime.Date.AddHours(20)
        });

        Assert.NotNull(replacement);
        Assert.Equal(table.Id, replacement.TableId);
    }

    [Fact]
    public async Task CreateReservationAsync_AutoAssignment_SelectsSmallestSuitableTable()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        // Table 1: Capacity 8
        context.RestaurantTables.Add(new RestaurantTable { TableNumber = 101, Capacity = 8, IsActive = true });
        // Table 2: Capacity 4
        context.RestaurantTables.Add(new RestaurantTable { TableNumber = 102, Capacity = 4, IsActive = true });
        // Table 3: Capacity 2 (too small for party of 4)
        context.RestaurantTables.Add(new RestaurantTable { TableNumber = 103, Capacity = 2, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Family Party",
            Email = "family@example.com",
            PartySize = 4, // Table 102 is the best fit (capacity 4)
            StartAtUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(19),
            DurationMinutes = 120
        };

        var result = await service.CreateReservationAsync(request);

        Assert.NotNull(result);
        Assert.Equal(102, result.TableNumber);
    }

    [Fact]
    public async Task CreateReservationAsync_WhenNoTableHasSufficientCapacity_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        context.RestaurantTables.Add(new RestaurantTable { TableNumber = 201, Capacity = 4, IsActive = true });
        await context.SaveChangesAsync();

        var request = new CreateReservationRequest
        {
            CustomerName = "Huge Group",
            Email = "group@example.com",
            PartySize = 10,
            StartAtUtc = DateTime.UtcNow.AddDays(1).Date.AddHours(19),
            DurationMinutes = 120
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateReservationAsync(request));

        Assert.Contains("capacity >= 10", ex.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidTransition_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new ReservationService(context);

        var table = new RestaurantTable { TableNumber = 301, Capacity = 4, IsActive = true };
        context.RestaurantTables.Add(table);
        await context.SaveChangesAsync();

        var res = await service.CreateReservationAsync(new CreateReservationRequest
        {
            CustomerName = "Bob",
            Email = "bob@example.com",
            PartySize = 2,
            TableId = table.Id,
            StartAtUtc = DateTime.UtcNow.AddHours(3),
            DurationMinutes = 60
        });

        // Cancel it
        await service.CancelReservationAsync(res.Id);

        // Transitioning from Cancelled to Seated is invalid
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateStatusAsync(res.Id, new UpdateReservationStatusRequest { Status = ReservationStatus.Seated }));
    }
}
