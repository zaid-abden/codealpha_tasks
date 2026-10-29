using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Reports;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Services;

public class ReportService : IReportService
{
    private readonly RestaurantDbContext _context;
    private readonly IInventoryService _inventoryService;

    public ReportService(RestaurantDbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    public async Task<DailySalesReportDto> GetDailySalesReportAsync(DateOnly date)
    {
        var startUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = date.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        // Revenue definition: Orders completed on this date (or created on this date and completed),
        // strictly excluding cancelled orders. Only OrderStatus.Completed counts as completed sales revenue.
        var salesQuery = _context.Orders
            .AsNoTracking()
            .Where(o => o.CreatedAtUtc >= startUtc && o.CreatedAtUtc <= endUtc && o.Status == OrderStatus.Completed);

        var groupedByType = await salesQuery
            .GroupBy(o => o.OrderType)
            .Select(g => new DailySalesTypeSummaryDto
            {
                OrderType = g.Key,
                OrderCount = g.Count(),
                TotalSales = g.Sum(o => o.Total)
            })
            .ToListAsync();

        var totalOrders = groupedByType.Sum(g => g.OrderCount);
        var totalSales = groupedByType.Sum(g => g.TotalSales);

        return new DailySalesReportDto
        {
            Date = date,
            TotalOrders = totalOrders,
            TotalSales = totalSales,
            SalesByType = groupedByType
        };
    }

    public async Task<List<LowStockReportDto>> GetLowStockReportAsync()
    {
        // Reuses the inventory service logic to avoid query duplication
        return await _inventoryService.GetLowStockItemsAsync();
    }

    public async Task<OrderSummaryReportDto> GetOrderSummaryAsync(DateTime? fromDateUtc, DateTime? toDateUtc)
    {
        var query = _context.Orders.AsNoTracking().AsQueryable();

        if (fromDateUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc >= fromDateUtc.Value.ToUniversalTime());
        }

        if (toDateUtc.HasValue)
        {
            query = query.Where(o => o.CreatedAtUtc <= toDateUtc.Value.ToUniversalTime());
        }

        var statusCounts = await query
            .GroupBy(o => o.Status)
            .Select(g => new OrderStatusCountDto
            {
                Status = g.Key,
                Count = g.Count()
            })
            .ToListAsync();

        var totalOrders = statusCounts.Sum(s => s.Count);

        return new OrderSummaryReportDto
        {
            FromDateUtc = fromDateUtc,
            ToDateUtc = toDateUtc,
            TotalOrders = totalOrders,
            CountsByStatus = statusCounts
        };
    }
}
