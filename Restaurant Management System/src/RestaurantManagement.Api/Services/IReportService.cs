using RestaurantManagement.Api.DTOs.Reports;

namespace RestaurantManagement.Api.Services;

public interface IReportService
{
    Task<DailySalesReportDto> GetDailySalesReportAsync(DateOnly date);
    Task<List<LowStockReportDto>> GetLowStockReportAsync();
    Task<OrderSummaryReportDto> GetOrderSummaryAsync(DateTime? fromDateUtc, DateTime? toDateUtc);
}
