using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Orders;

public class OrderFilterParams : PaginationParams
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public OrderStatus? Status { get; set; }
    public OrderType? OrderType { get; set; }
}
