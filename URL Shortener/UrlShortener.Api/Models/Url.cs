namespace UrlShortener.Api.Models;

/// <summary>
/// Represents a shortened URL record stored in the database.
/// </summary>
public class Url
{
    /// <summary>
    /// Unique auto-incrementing identifier for the URL record.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The unique random short code representing the original URL.
    /// </summary>
    public string ShortCode { get; set; } = string.Empty;

    /// <summary>
    /// The original destination URL.
    /// </summary>
    public string OriginalUrl { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the URL was created, stored in UTC.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
