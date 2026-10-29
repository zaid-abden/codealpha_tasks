using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Api.Data;

namespace RestaurantManagement.Tests.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public const string TestConnectionString = "Server=(localdb)\\MSSQLLocalDB;Database=RestaurantManagement_ApiTestDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", TestConnectionString);

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                var options = new DbContextOptionsBuilder<RestaurantDbContext>()
                    .UseSqlServer(TestConnectionString)
                    .Options;
                using var db = new RestaurantDbContext(options);
                db.Database.EnsureDeleted();
            }
            catch
            {
                // Ignore cleanup errors on disposal
            }
        }
    }
}
