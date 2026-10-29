using UrlShortener.Api.DTOs;

namespace UrlShortener.Api.Services;

/// <summary>
/// Service contract defining URL shortener operations.
/// </summary>
public interface IUrlShortenerService
{
    /// <summary>
    /// Creates and persists a new short URL record for the provided long URL.
    /// </summary>
    /// <param name="request">Request containing the long URL.</param>
    /// <param name="baseUrl">Base URL of the running API (e.g., https://localhost:7001).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Response with short URL metadata.</returns>
    Task<ShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request, string baseUrl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the original long URL associated with a given short code.
    /// </summary>
    /// <param name="shortCode">The short code to search for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Original URL if found; otherwise null.</returns>
    Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves detailed metadata for a given short code.
    /// </summary>
    /// <param name="shortCode">The short code to search for.</param>
    /// <param name="baseUrl">Base URL of the running API.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ShortUrlResponse if found; otherwise null.</returns>
    Task<ShortUrlResponse?> GetUrlDetailsAsync(string shortCode, string baseUrl, CancellationToken cancellationToken = default);
}
