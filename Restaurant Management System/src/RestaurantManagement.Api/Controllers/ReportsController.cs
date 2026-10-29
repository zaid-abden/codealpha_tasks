using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Reports;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    /// <summary>
    /// Retrieve daily sales report by date (YYYY-MM-DD), grouped by order type, strictly excluding cancelled orders.
    /// </summary>
    [HttpGet("daily-sales")]
    [ProducesResponseType(typeof(DailySalesReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<DailySalesReportDto>> GetDailySales([FromQuery] DateOnly? date)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var report = await _reportService.GetDailySalesReportAsync(targetDate);
        return Ok(report);
    }

    /// <summary>
    /// Retrieve list of all inventory items that are currently at or below their reorder threshold.
    /// </summary>
    [HttpGet("low-stock")]
    [ProducesResponseType(typeof(List<LowStockReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<LowStockReportDto>>> GetLowStock()
    {
        var report = await _reportService.GetLowStockReportAsync();
        return Ok(report);
    }

    /// <summary>
    /// Retrieve aggregate order counts grouped by status across an optional date range.
    /// </summary>
    [HttpGet("order-summary")]
    [ProducesResponseType(typeof(OrderSummaryReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OrderSummaryReportDto>> GetOrderSummary(
        [FromQuery] DateTime? fromDateUtc,
        [FromQuery] DateTime? toDateUtc)
    {
        var report = await _reportService.GetOrderSummaryAsync(fromDateUtc, toDateUtc);
        return Ok(report);
    }
}
