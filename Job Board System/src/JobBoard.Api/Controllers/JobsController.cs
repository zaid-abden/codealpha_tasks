using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JobBoard.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Produces("application/json")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IApplicationService _applicationService;

    public JobsController(IJobService jobService, IApplicationService applicationService)
    {
        _jobService = jobService;
        _applicationService = applicationService;
    }

    /// <summary>
    /// Returns a paginated list of published jobs with optional search, filters, and sorting.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<JobResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResultDto<JobResponseDto>>> GetJobs([FromQuery] JobFilterDto filter, CancellationToken cancellationToken)
    {
        var result = await _jobService.GetPublishedJobsAsync(filter, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Returns the details of a specific publicly available published job.
    /// </summary>
    [HttpGet("{id:int}", Name = "GetJobById")]
    [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobResponseDto>> GetJobById(int id, CancellationToken cancellationToken)
    {
        var result = await _jobService.GetJobByIdAsync(id, publicOnly: true, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Creates a new job listing with initial Draft status.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JobResponseDto>> CreateJob([FromBody] JobCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _jobService.CreateJobAsync(dto, cancellationToken);
        return CreatedAtAction(nameof(GetJobById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Updates an existing job listing.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobResponseDto>> UpdateJob(int id, [FromBody] JobUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await _jobService.UpdateJobAsync(id, dto, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Deletes a job or soft-deletes (archives) it if applications already exist.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteJob(int id, CancellationToken cancellationToken)
    {
        await _jobService.DeleteJobAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Publishes a valid draft job so it appears in search results and can accept applications.
    /// </summary>
    [HttpPatch("{id:int}/publish")]
    [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobResponseDto>> PublishJob(int id, CancellationToken cancellationToken)
    {
        var result = await _jobService.PublishJobAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Closes a published job and prevents further applications.
    /// </summary>
    [HttpPatch("{id:int}/close")]
    [ProducesResponseType(typeof(JobResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobResponseDto>> CloseJob(int id, CancellationToken cancellationToken)
    {
        var result = await _jobService.CloseJobAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Submits a job application for the specified published job.
    /// </summary>
    [HttpPost("{jobId:int}/applications")]
    [ProducesResponseType(typeof(ApplicationResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ApplicationResponseDto>> ApplyForJob(int jobId, [FromBody] ApplicationCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await _applicationService.ApplyForJobAsync(jobId, dto, cancellationToken);
        return CreatedAtAction("GetApplicationById", "Applications", new { id = result.Id }, result);
    }

    /// <summary>
    /// Returns a paginated list of applications for the specified job.
    /// Note: Access control is omitted in this unauthenticated demo.
    /// </summary>
    [HttpGet("{jobId:int}/applications")]
    [ProducesResponseType(typeof(PagedResultDto<ApplicationResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResultDto<ApplicationResponseDto>>> GetJobApplications(
        int jobId,
        [FromQuery] PaginationQueryDto pagination,
        CancellationToken cancellationToken)
    {
        var result = await _applicationService.GetApplicationsForJobAsync(jobId, pagination, cancellationToken);
        return Ok(result);
    }
}
