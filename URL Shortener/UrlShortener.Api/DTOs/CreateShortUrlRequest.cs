using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Api.DTOs;

/// <summary>
/// Request model for shortening a URL.
/// </summary>
public class CreateShortUrlRequest
{
    /// <summary>
    /// The destination long URL to be shortened (must use HTTP or HTTPS scheme).
    /// </summary>
    [Required(ErrorMessage = "The longUrl field is required.")]
    public string LongUrl { get; set; } = string.Empty;
}
