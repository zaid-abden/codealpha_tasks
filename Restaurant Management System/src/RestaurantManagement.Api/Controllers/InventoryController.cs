using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Inventory;
using RestaurantManagement.Api.DTOs.Reports;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class InventoryController : ControllerBase
{
    private readonly IInventoryService _inventoryService;

    public InventoryController(IInventoryService inventoryService)
    {
        _inventoryService = inventoryService;
    }

    /// <summary>
    /// Retrieve paginated inventory items with optional keyword and low-stock filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<InventoryItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<InventoryItemDto>>> GetInventory([FromQuery] InventoryFilterParams filter)
    {
        var result = await _inventoryService.GetInventoryItemsAsync(filter);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve inventory items whose current stock is at or below their reorder threshold.
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<LowStockReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LowStockReportDto>>> GetLowStock()
    {
        var items = await _inventoryService.GetLowStockItemsAsync();
        return Ok(items);
    }

    /// <summary>
    /// Retrieve details for a single inventory item by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InventoryItemDto>> GetInventoryItem(int id)
    {
        var item = await _inventoryService.GetInventoryItemByIdAsync(id);
        return Ok(item);
    }

    /// <summary>
    /// Retrieve audit trail of inventory movements for an item.
    /// </summary>
    [HttpGet("{id:int}/movements")]
    [ProducesResponseType(typeof(List<InventoryMovementDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<InventoryMovementDto>>> GetMovements(int id)
    {
        var movements = await _inventoryService.GetMovementsByItemIdAsync(id);
        return Ok(movements);
    }

    /// <summary>
    /// Create a new inventory item with initial stock and reorder threshold.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemDto>> CreateInventoryItem([FromBody] CreateInventoryItemRequest request)
    {
        var created = await _inventoryService.CreateInventoryItemAsync(request);
        return CreatedAtAction(nameof(GetInventoryItem), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update metadata (name, unit, threshold, active status) of an inventory item without overwriting stock levels.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemDto>> UpdateInventoryItem(int id, [FromBody] UpdateInventoryItemRequest request)
    {
        var updated = await _inventoryService.UpdateInventoryItemAsync(id, request);
        return Ok(updated);
    }

    /// <summary>
    /// Record an inventory stock adjustment (positive or negative) with an immutable audit log.
    /// </summary>
    [HttpPost("{id:int}/adjustments")]
    [ProducesResponseType(typeof(InventoryItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<InventoryItemDto>> AdjustStock(int id, [FromBody] InventoryAdjustmentRequest request)
    {
        var updated = await _inventoryService.AdjustStockAsync(id, request);
        return Ok(updated);
    }
}
