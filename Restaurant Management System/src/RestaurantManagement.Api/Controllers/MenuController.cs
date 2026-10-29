using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Menu;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MenuController : ControllerBase
{
    private readonly IMenuService _menuService;

    public MenuController(IMenuService menuService)
    {
        _menuService = menuService;
    }

    /// <summary>
    /// Retrieve paginated active menu items with optional category and keyword filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<MenuItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<MenuItemDto>>> GetMenu([FromQuery] MenuFilterParams filter)
    {
        var result = await _menuService.GetMenuItemsAsync(filter);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve details for a single active menu item.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> GetMenuItem(int id)
    {
        var item = await _menuService.GetMenuItemByIdAsync(id);
        return Ok(item);
    }

    /// <summary>
    /// Create a new menu item with optional recipe ingredients.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MenuItemDto>> CreateMenuItem([FromBody] CreateMenuItemRequest request)
    {
        var created = await _menuService.CreateMenuItemAsync(request);
        return CreatedAtAction(nameof(GetMenuItem), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update details, price, and ingredients for an existing menu item.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> UpdateMenuItem(int id, [FromBody] UpdateMenuItemRequest request)
    {
        var updated = await _menuService.UpdateMenuItemAsync(id, request);
        return Ok(updated);
    }

    /// <summary>
    /// Change availability status for a menu item.
    /// </summary>
    [HttpPatch("{id:int}/availability")]
    [ProducesResponseType(typeof(MenuItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MenuItemDto>> UpdateAvailability(int id, [FromBody] UpdateMenuItemAvailabilityRequest request)
    {
        var updated = await _menuService.UpdateAvailabilityAsync(id, request.IsAvailable);
        return Ok(updated);
    }

    /// <summary>
    /// Deactivate or delete a menu item. Deactivates if referenced by historical orders.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMenuItem(int id)
    {
        await _menuService.DeleteMenuItemAsync(id);
        return NoContent();
    }
}
