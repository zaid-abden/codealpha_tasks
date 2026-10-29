namespace UrlShortener.Api.DTOs;

/// <summary>
/// Response model returned when a short URL is created or retrieved.
/// </summary>
public class ShortUrlResponse
{
    /// <summary>
    /// Unique database identifier for the URL record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The generated unique short code.
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>
    /// The original long URL.
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// The fully qualified short URL including the host domain and short code.
    /// </summary>
    public string ShortUrl { get; set; } = string.Empty;

    /// <summary>
    /// The UTC timestamp when the record was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
