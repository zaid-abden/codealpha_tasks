using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Orders;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Retrieve paginated orders with optional date range, status, and order type filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<OrderDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<OrderDto>>> GetOrders([FromQuery] OrderFilterParams filter)
    {
        var result = await _orderService.GetOrdersAsync(filter);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve details for a single order by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrderDto>> GetOrder(int id)
    {
        var order = await _orderService.GetOrderByIdAsync(id);
        return Ok(order);
    }

    /// <summary>
    /// Create a new order with atomic price snapshot and total calculations.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> CreateOrder([FromBody] CreateOrderRequest request)
    {
        var created = await _orderService.CreateOrderAsync(request);
        return CreatedAtAction(nameof(GetOrder), new { id = created.Id }, created);
    }

    /// <summary>
    /// Add items to an existing eligible pending order.
    /// </summary>
    [HttpPost("{id:int}/items")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> AddItemsToOrder(int id, [FromBody] AddOrderItemsRequest request)
    {
        var updated = await _orderService.AddItemsToOrderAsync(id, request);
        return Ok(updated);
    }

    /// <summary>
    /// Update order status. Transition to Preparing triggers atomic inventory deduction.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OrderDto>> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
    {
        var updated = await _orderService.UpdateOrderStatusAsync(id, request);
        return Ok(updated);
    }
}
