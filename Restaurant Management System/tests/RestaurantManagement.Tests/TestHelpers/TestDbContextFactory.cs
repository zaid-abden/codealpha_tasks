using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;

namespace RestaurantManagement.Tests.TestHelpers;

public static class TestDbContextFactory
{
    public static RestaurantDbContext CreateInMemory(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<RestaurantDbContext>()
            .UseInMemoryDatabase(databaseName: dbName ?? Guid.NewGuid().ToString())
            .EnableSensitiveDataLogging()
            .Options;

        var context = new RestaurantDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
