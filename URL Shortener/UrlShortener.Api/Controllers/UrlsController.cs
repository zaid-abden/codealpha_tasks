using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.DTOs;
using UrlShortener.Api.Services;

namespace UrlShortener.Api.Controllers;

/// <summary>
/// API controller exposing endpoints to create short URLs, retrieve URL metadata, and redirect.
/// </summary>
[ApiController]
public class UrlsController : ControllerBase
{
    private readonly IUrlShortenerService _urlShortenerService;
    private readonly ILogger<UrlsController> _logger;

    public UrlsController(IUrlShortenerService urlShortenerService, ILogger<UrlsController> logger)
    {
        _urlShortenerService = urlShortenerService;
        _logger = logger;
    }

    /// <summary>
    /// Creates a shortened URL from a given destination long URL.
    /// </summary>
    /// <param name="request">Request body containing the long URL.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 201 Created with the generated short code and metadata.</returns>
    [HttpPost("api/urls")]
    [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> CreateShortUrl([FromBody] CreateShortUrlRequest request, CancellationToken cancellationToken)
    {
        if (request == null || !ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
            var response = await _urlShortenerService.CreateShortUrlAsync(request, baseUrl, cancellationToken);

            return CreatedAtAction(
                nameof(GetUrlDetails),
                new { shortCode = response.ShortCode },
                response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Validation failure while creating short URL.");
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error occurred while creating short URL.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                error = "An unexpected error occurred while processing your request. Please try again later."
            });
        }
    }

    /// <summary>
    /// Retrieves metadata and statistics for an existing short code.
    /// </summary>
    /// <param name="shortCode">The unique short code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 200 OK with URL details, or 404 Not Found.</returns>
    [HttpGet("api/urls/{shortCode}")]
    [ProducesResponseType(typeof(ShortUrlResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUrlDetails([FromRoute] string shortCode, CancellationToken cancellationToken)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var details = await _urlShortenerService.GetUrlDetailsAsync(shortCode, baseUrl, cancellationToken);

        if (details == null)
        {
            return NotFound(new { error = $"Short URL code '{shortCode}' was not found." });
        }

        return Ok(details);
    }

    /// <summary>
    /// Redirects the user to the original destination URL associated with the short code.
    /// </summary>
    /// <param name="shortCode">The unique short code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP 302 Found redirect, or 404 Not Found.</returns>
    [HttpGet("/{shortCode}")]
    [ApiExplorerSettings(IgnoreApi = false)]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RedirectToOriginal([FromRoute] string shortCode, CancellationToken cancellationToken)
    {
        // Guard against intercepting reserved paths or API routes
        if (string.IsNullOrWhiteSpace(shortCode) ||
            shortCode.Equals("swagger", StringComparison.OrdinalIgnoreCase) ||
            shortCode.Equals("api", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = $"Resource '{shortCode}' was not found." });
        }

        var originalUrl = await _urlShortenerService.GetOriginalUrlAsync(shortCode, cancellationToken);

        if (originalUrl == null)
        {
            return NotFound(new { error = $"Short URL code '{shortCode}' was not found." });
        }

        // HTTP 302 Found redirect
        return Redirect(originalUrl);
    }
}
