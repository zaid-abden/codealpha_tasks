using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using Xunit;

namespace RestaurantManagement.Tests.IntegrationTests;

public class SqlServerRelationalIntegrationTests : IAsyncLifetime
{
    private readonly RestaurantDbContext _context;
    private const string ConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RestaurantManagement_TestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    public SqlServerRelationalIntegrationTests()
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseSqlServer(ConnectionString)
            .EnableSensitiveDataLogging()
            .Options;

        _context = new RestaurantDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.Database.EnsureDeletedAsync();
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task SqlServer_EnforcesUniqueIndex_OnTableNumber()
    {
        var table1 = new RestaurantTable { TableNumber = 901, Capacity = 4, IsActive = true };
        var table2 = new RestaurantTable { TableNumber = 901, Capacity = 2, IsActive = true };

        _context.RestaurantTables.Add(table1);
        await _context.SaveChangesAsync();

        _context.RestaurantTables.Add(table2);

        // SQL Server unique index IX_RestaurantTables_TableNumber must trigger DbUpdateException
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task SqlServer_EnforcesUniqueIndex_OnNormalizedInventoryName()
    {
        var item1 = new InventoryItem
        {
            Name = "Organic Milk",
            NormalizedName = "ORGANIC MILK",
            Unit = "L",
            CurrentQuantity = 10m,
            ReorderThreshold = 2m,
            IsActive = true
        };
        var item2 = new InventoryItem
        {
            Name = "organic milk",
            NormalizedName = "ORGANIC MILK",
            Unit = "L",
            CurrentQuantity = 5m,
            ReorderThreshold = 1m,
            IsActive = true
        };

        _context.InventoryItems.Add(item1);
        await _context.SaveChangesAsync();

        _context.InventoryItems.Add(item2);

        // SQL Server unique index IX_InventoryItems_NormalizedName must trigger DbUpdateException
        await Assert.ThrowsAsync<DbUpdateException>(() => _context.SaveChangesAsync());
    }

    [Fact]
    public async Task SqlServer_TransactionsAndRollback_PreserveDataIntegrity()
    {
        var item = new InventoryItem
        {
            Name = "Butter",
            NormalizedName = "BUTTER",
            Unit = "kg",
            CurrentQuantity = 10m,
            ReorderThreshold = 2m,
            IsActive = true
        };
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        // Begin transaction, perform modification, then rollback
        await using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            item.CurrentQuantity = 3m;
            await _context.SaveChangesAsync();

            await transaction.RollbackAsync();
        }

        // Detach and reload to verify rollback in SQL Server
        _context.Entry(item).State = EntityState.Detached;
        var reloaded = await _context.InventoryItems.FindAsync(item.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(10m, reloaded.CurrentQuantity);
    }
}
