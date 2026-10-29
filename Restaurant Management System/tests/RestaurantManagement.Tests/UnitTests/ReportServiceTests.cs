using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;
using RestaurantManagement.Api.Services;
using RestaurantManagement.Tests.TestHelpers;
using Xunit;

namespace RestaurantManagement.Tests.UnitTests;

public class ReportServiceTests
{
    [Fact]
    public async Task GetDailySalesReportAsync_CalculatesCompletedSalesAndExcludesCancelledOrders()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var inventoryService = new InventoryService(context);
        var reportService = new ReportService(context, inventoryService);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayDate = today.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

        // Completed DineIn order: $40.00
        context.Orders.Add(new Order
        {
            OrderNumber = "ORD-001",
            OrderType = OrderType.DineIn,
            Status = OrderStatus.Completed,
            Subtotal = 40.00m,
            Total = 40.00m,
            CreatedAtUtc = todayDate
        });

        // Completed Takeaway order: $25.50
        context.Orders.Add(new Order
        {
            OrderNumber = "ORD-002",
            OrderType = OrderType.Takeaway,
            Status = OrderStatus.Completed,
            Subtotal = 25.50m,
            Total = 25.50m,
            CreatedAtUtc = todayDate
        });

        // Cancelled DineIn order: $100.00 (MUST BE EXCLUDED!)
        context.Orders.Add(new Order
        {
            OrderNumber = "ORD-003",
            OrderType = OrderType.DineIn,
            Status = OrderStatus.Cancelled,
            Subtotal = 100.00m,
            Total = 100.00m,
            CreatedAtUtc = todayDate
        });

        // Pending DineIn order: $50.00 (Not completed, excluded from completed sales)
        context.Orders.Add(new Order
        {
            OrderNumber = "ORD-004",
            OrderType = OrderType.DineIn,
            Status = OrderStatus.Pending,
            Subtotal = 50.00m,
            Total = 50.00m,
            CreatedAtUtc = todayDate
        });

        await context.SaveChangesAsync();

        var report = await reportService.GetDailySalesReportAsync(today);

        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(65.50m, report.TotalSales);
        Assert.Equal(2, report.SalesByType.Count);

        var dineInSales = report.SalesByType.First(s => s.OrderType == OrderType.DineIn);
        Assert.Equal(1, dineInSales.OrderCount);
        Assert.Equal(40.00m, dineInSales.TotalSales);

        var takeawaySales = report.SalesByType.First(s => s.OrderType == OrderType.Takeaway);
        Assert.Equal(1, takeawaySales.OrderCount);
        Assert.Equal(25.50m, takeawaySales.TotalSales);
    }

    [Fact]
    public async Task GetOrderSummaryAsync_GroupsCountsByStatus()
    {
        using var context = TestDbContextFactory.CreateInMemory();
        var inventoryService = new InventoryService(context);
        var reportService = new ReportService(context, inventoryService);

        var now = DateTime.UtcNow;

        context.Orders.AddRange(
            new Order { OrderNumber = "O1", Status = OrderStatus.Pending, CreatedAtUtc = now },
            new Order { OrderNumber = "O2", Status = OrderStatus.Pending, CreatedAtUtc = now },
            new Order { OrderNumber = "O3", Status = OrderStatus.Preparing, CreatedAtUtc = now },
            new Order { OrderNumber = "O4", Status = OrderStatus.Completed, CreatedAtUtc = now },
            new Order { OrderNumber = "O5", Status = OrderStatus.Cancelled, CreatedAtUtc = now }
        );
        await context.SaveChangesAsync();

        var summary = await reportService.GetOrderSummaryAsync(now.AddHours(-1), now.AddHours(1));

        Assert.Equal(5, summary.TotalOrders);
        var pendingCount = summary.CountsByStatus.First(c => c.Status == OrderStatus.Pending).Count;
        var preparingCount = summary.CountsByStatus.First(c => c.Status == OrderStatus.Preparing).Count;
        Assert.Equal(2, pendingCount);
        Assert.Equal(1, preparingCount);
    }
}
