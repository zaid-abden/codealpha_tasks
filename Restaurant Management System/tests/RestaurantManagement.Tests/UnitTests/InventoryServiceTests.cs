using RestaurantManagement.Api.DTOs.Inventory;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class InventoryServiceTests
{
    [Fact]
    public async Task CreateInventoryItemAsync_CreatesItemAndInitialMovement()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new InventoryService(context);

        var request = new CreateInventoryItemRequest
        {
            Name = "Olive Oil",
            Unit = "L",
            CurrentQuantity = 25.5m,
            ReorderThreshold = 5.0m
        };

        var result = await service.CreateInventoryItemAsync(request);

        Assert.NotNull(result);
        Assert.Equal("Olive Oil", result.Name);
        Assert.Equal("OLIVE OIL", result.NormalizedName);
        Assert.Equal(25.5m, result.CurrentQuantity);
        Assert.False(result.IsLowStock);

        var movements = await service.GetMovementsByItemIdAsync(result.Id);
        Assert.Single(movements);
        Assert.Equal(InventoryMovementType.InitialStock, movements[0].MovementType);
        Assert.Equal(25.5m, movements[0].QuantityChange);
    }

    [Fact]
    public async Task CreateInventoryItemAsync_WithDuplicateNormalizedName_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new InventoryService(context);

        await service.CreateInventoryItemAsync(new CreateInventoryItemRequest
        {
            Name = "Tomato Sauce",
            Unit = "kg",
            CurrentQuantity = 10m,
            ReorderThreshold = 2m
        });

        // Exact match with different casing or whitespace
        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateInventoryItemAsync(new CreateInventoryItemRequest
            {
                Name = "  tomato sauce  ",
                Unit = "kg",
                CurrentQuantity = 5m,
                ReorderThreshold = 1m
            }));
    }

    [Fact]
    public async Task AdjustStockAsync_WithValidPositiveAndNegativeChanges_UpdatesStockAndRecordsMovements()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new InventoryService(context);

        var item = await service.CreateInventoryItemAsync(new CreateInventoryItemRequest
        {
            Name = "Sugar",
            Unit = "kg",
            CurrentQuantity = 50m,
            ReorderThreshold = 10m
        });

        // Add 20kg restock
        var afterAdd = await service.AdjustStockAsync(item.Id, new InventoryAdjustmentRequest
        {
            QuantityChange = 20m,
            Reason = "Weekly supplier shipment"
        });
        Assert.Equal(70m, afterAdd.CurrentQuantity);

        // Deduct 15kg for kitchen batch
        var afterDeduct = await service.AdjustStockAsync(item.Id, new InventoryAdjustmentRequest
        {
            QuantityChange = -15m,
            Reason = "Batch baking"
        });
        Assert.Equal(55m, afterDeduct.CurrentQuantity);

        var movements = await service.GetMovementsByItemIdAsync(item.Id);
        Assert.Equal(3, movements.Count); // 1 Initial + 1 Restock + 1 ManualAdjustment
    }

    [Fact]
    public async Task AdjustStockAsync_WhenAdjustmentExceedsCurrentStock_ThrowsConflictException()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new InventoryService(context);

        var item = await service.CreateInventoryItemAsync(new CreateInventoryItemRequest
        {
            Name = "Flour",
            Unit = "kg",
            CurrentQuantity = 5m,
            ReorderThreshold = 10m
        });

        // Attempt to deduct 10kg from 5kg stock
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            service.AdjustStockAsync(item.Id, new InventoryAdjustmentRequest
            {
                QuantityChange = -10m,
                Reason = "Excess deduction"
            }));

        Assert.Contains("negative stock", ex.Message);

        var refreshed = await service.GetInventoryItemByIdAsync(item.Id);
        Assert.Equal(5m, refreshed.CurrentQuantity); // Unchanged
    }

    [Fact]
    public async Task GetLowStockItemsAsync_ReturnsOnlyItemsAtOrBelowThreshold()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var service = new InventoryService(context);

        // Low stock: 4 <= 5
        await service.CreateInventoryItemAsync(new CreateInventoryItemRequest
        {
            Name = "Milk",
            Unit = "L",
            CurrentQuantity = 4m,
            ReorderThreshold = 5m
        });

        // Normal stock: 20 > 5
        await service.CreateInventoryItemAsync(new CreateInventoryItemRequest
        {
            Name = "Eggs",
            Unit = "pcs",
            CurrentQuantity = 20m,
            ReorderThreshold = 5m
        });

        var lowStock = await service.GetLowStockItemsAsync();

        Assert.Single(lowStock);
        Assert.Equal("Milk", lowStock[0].Name);
        Assert.Equal(1m, lowStock[0].Deficit);
    }
}
