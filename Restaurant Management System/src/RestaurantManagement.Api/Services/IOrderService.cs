using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Orders;

namespace RestaurantManagement.Api.Services;

public interface IOrderService
{
    Task<PagedResult<OrderDto>> GetOrdersAsync(OrderFilterParams filter);
    Task<OrderDto> GetOrderByIdAsync(int id);
    Task<OrderDto> CreateOrderAsync(CreateOrderRequest request);
    Task<OrderDto> AddItemsToOrderAsync(int id, AddOrderItemsRequest request);
    Task<OrderDto> UpdateOrderStatusAsync(int id, UpdateOrderStatusRequest request);
}
