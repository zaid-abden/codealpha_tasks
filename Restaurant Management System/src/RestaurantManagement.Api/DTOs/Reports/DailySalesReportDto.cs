using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Reports;

public class DailySalesTypeSummaryDto
{
    public OrderType OrderType { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalSales { get; set; }
}

public class DailySalesReportDto
{
    public DateOnly Date { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalSales { get; set; }
    public List<DailySalesTypeSummaryDto> SalesByType { get; set; } = new();
}
