using EventRegistration.Api.Dtos.Registrations;
using EventRegistration.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EventRegistration.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RegistrationsController : ControllerBase
{
    private readonly IRegistrationService _registrationService;

    public RegistrationsController(IRegistrationService registrationService)
    {
        _registrationService = registrationService;
    }

    /// <summary>
    /// Returns registration details by ID.
    /// </summary>
    [HttpGet("{id:int}", Name = nameof(GetRegistrationById))]
    [ProducesResponseType(typeof(RegistrationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RegistrationResponse>> GetRegistrationById(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        var result = await _registrationService.GetRegistrationByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cancels a registration before the event starts.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelRegistration(
        [FromRoute] int id,
        CancellationToken cancellationToken)
    {
        await _registrationService.CancelRegistrationAsync(id, cancellationToken);
        return NoContent();
    }
}
