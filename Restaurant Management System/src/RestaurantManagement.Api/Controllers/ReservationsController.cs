using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Reservations;
using RestaurantManagement.Api.Services;

namespace RestaurantManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ReservationsController : ControllerBase
{
    private readonly IReservationService _reservationService;

    public ReservationsController(IReservationService reservationService)
    {
        _reservationService = reservationService;
    }

    /// <summary>
    /// Retrieve paginated list of reservations with optional date, status, and table filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReservationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ReservationDto>>> GetReservations([FromQuery] ReservationFilterParams filter)
    {
        var result = await _reservationService.GetReservationsAsync(filter);
        return Ok(result);
    }

    /// <summary>
    /// Retrieve details for a single reservation.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReservationDto>> GetReservation(int id)
    {
        var reservation = await _reservationService.GetReservationByIdAsync(id);
        return Ok(reservation);
    }

    /// <summary>
    /// Create a new reservation with automatic or specific table assignment.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> CreateReservation([FromBody] CreateReservationRequest request)
    {
        var created = await _reservationService.CreateReservationAsync(request);
        return CreatedAtAction(nameof(GetReservation), new { id = created.Id }, created);
    }

    /// <summary>
    /// Update reservation lifecycle status (Pending, Confirmed, Seated, Completed, Cancelled).
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> UpdateStatus(int id, [FromBody] UpdateReservationStatusRequest request)
    {
        var updated = await _reservationService.UpdateStatusAsync(id, request);
        return Ok(updated);
    }

    /// <summary>
    /// Cancel a reservation and release the table.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ReservationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ReservationDto>> CancelReservation(int id)
    {
        var cancelled = await _reservationService.CancelReservationAsync(id);
        return Ok(cancelled);
    }
}
