using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Tables;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TablesController : ControllerBase
{
    private readonly ITableService _tableService;

    public TablesController(ITableService tableService)
    {
        _tableService = tableService;
    }

    /// <summary>
    /// Retrieve all restaurant tables with optional capacity and status filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<RestaurantTableDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<RestaurantTableDto>>> GetTables([FromQuery] TableFilterParams filter)
    {
        var tables = await _tableService.GetTablesAsync(filter);
        return Ok(tables);
    }

    /// <summary>
    /// Retrieve details for a single table.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RestaurantTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RestaurantTableDto>> GetTable(int id)
    {
        var table = await _tableService.GetTableByIdAsync(id);
        return Ok(table);
    }

    /// <summary>
    /// Create a new restaurant table.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(RestaurantTableDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RestaurantTableDto>> CreateTable([FromBody] CreateTableRequest request)
    {
        var created = await _tableService.CreateTableAsync(request);
        return CreatedAtAction(nameof(GetTable), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update table configuration, seating capacity, or active status.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(RestaurantTableDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RestaurantTableDto>> UpdateTable(int id, [FromBody] UpdateTableRequest request)
    {
        var updated = await _tableService.UpdateTableAsync(id, request);
        return Ok(updated);
    }
}
