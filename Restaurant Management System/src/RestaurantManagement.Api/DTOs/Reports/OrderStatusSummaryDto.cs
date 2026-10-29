using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Reports;

public class OrderStatusCountDto
{
    public OrderStatus Status { get; set; }
    public int Count { get; set; }
}

public class OrderSummaryReportDto
{
    public DateTime? FromDateUtc { get; set; }
    public DateTime? ToDateUtc { get; set; }
    public int TotalOrders { get; set; }
    public List<OrderStatusCountDto> CountsByStatus { get; set; } = new();
}
