using RestaurantManagement.Api.DTOs.Tables;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class TableServiceTests
{
    [Fact]
    public async Task CreateTableAsync_WithValidData_CreatesTable()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new TableService(context);

        var request = new CreateTableRequest { TableNumber = 5, Capacity = 4 };
        var result = await service.CreateTableAsync(request);

        Assert.NotNull(result);
        Assert.Equal(5, result.TableNumber);
        Assert.Equal(4, result.Capacity);
        Assert.True(result.IsActive);
    }

    [Fact]
    public async Task CreateTableAsync_WithDuplicateTableNumber_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new TableService(context);

        await service.CreateTableAsync(new CreateTableRequest { TableNumber = 10, Capacity = 2 });

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateTableAsync(new CreateTableRequest { TableNumber = 10, Capacity = 4 }));
    }

    [Fact]
    public async Task UpdateTableAsync_DeactivatingWithActiveReservations_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new TableService(context);

        var table = await service.CreateTableAsync(new CreateTableRequest { TableNumber = 20, Capacity = 6 });

        // Add future active reservation
        context.Reservations.Add(new Reservation
        {
            CustomerName = "John Doe",
            TableId = table.Id,
            PartySize = 4,
            StartAtUtc = DateTime.UtcNow.AddHours(2),
            EndAtUtc = DateTime.UtcNow.AddHours(4),
            Status = ReservationStatus.Confirmed
        });
        await context.SaveChangesAsync();

        // Attempt deactivation
        var updateRequest = new UpdateTableRequest
        {
            TableNumber = 20,
            Capacity = 6,
            IsActive = false
        };

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.UpdateTableAsync(table.Id, updateRequest));

        Assert.Contains("active upcoming reservations", ex.Message);
    }

    [Fact]
    public async Task UpdateTableAsync_DeactivatingWithNoActiveReservations_Succeeds()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new TableService(context);

        var table = await service.CreateTableAsync(new CreateTableRequest { TableNumber = 30, Capacity = 4 });

        var updateRequest = new UpdateTableRequest
        {
            TableNumber = 30,
            Capacity = 4,
            IsActive = false
        };

        var updated = await service.UpdateTableAsync(table.Id, updateRequest);
        Assert.False(updated.IsActive);
    }
}
