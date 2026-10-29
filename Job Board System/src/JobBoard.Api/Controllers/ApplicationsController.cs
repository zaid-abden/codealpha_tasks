using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Api.Controllers;

[ApiController]
[Route("api/applications")]
[Produces("application/json")]
public class ApplicationsController : ControllerBase
{
    private readonly IApplicationService _applicationService;

    public ApplicationsController(IApplicationService applicationService)
    {
        _applicationService = applicationService;
    }

    /// <summary>
    /// Returns the details of a specific job application.
    /// Note: Access control is omitted in this unauthenticated demo.
    /// </summary>
    [HttpGet("{id:int}", Name = "GetApplicationById")]
    [ProducesResponseType(typeof(ApplicationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApplicationResponseDto>> GetApplicationById(int id, CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetApplicationByIdAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Updates the review status of an application according to allowed workflow transitions.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType(typeof(ApplicationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponseDto>> UpdateStatus(
        int id,
        [FromBody] ApplicationStatusUpdateDto dto,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.UpdateApplicationStatusAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Allows an applicant to withdraw an active application (soft withdrawal).
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApplicationResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponseDto>> WithdrawApplication(int id, CancellationToken cancellationToken)
    {
        var result = await _applicationService.WithdrawApplicationAsync(id, cancellationToken);
        return Ok(result);
    }
}
