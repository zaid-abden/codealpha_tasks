using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using UrlShortener.Api.Data;
using UrlShortener.Api.DTOs;
using UrlShortener.Api.Models;

namespace UrlShortener.Api.Services;

/// <summary>
/// Implements URL shortening, validation, retrieval, and cryptographic code generation.
/// </summary>
public class UrlShortenerService : IUrlShortenerService
{
    private const string Base62Alphabet = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private const int CodeLength = 7;
    private const int MaxGenerationAttempts = 5;

    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UrlShortenerService> _logger;

    public UrlShortenerService(ApplicationDbContext dbContext, ILogger<UrlShortenerService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShortUrlResponse> CreateShortUrlAsync(CreateShortUrlRequest request, string baseUrl, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Validate the URL format and scheme (HTTP or HTTPS only)
        var validatedUrl = ValidateUrl(request.LongUrl);

        // 2. Generate a unique short code with collision handling and database retry
        for (var attempt = 1; attempt <= MaxGenerationAttempts; attempt++)
        {
            var shortCode = GenerateSecureCode(CodeLength);

            // Check if the short code already exists in the database
            var codeExists = await _dbContext.Urls
                .AnyAsync(u => u.ShortCode == shortCode, cancellationToken);

            if (codeExists)
            {
                _logger.LogWarning("Short code collision detected for '{Code}'. Retrying ({Attempt}/{MaxAttempts})...", shortCode, attempt, MaxGenerationAttempts);
                continue;
            }

            var urlEntity = new Url
            {
                ShortCode = shortCode,
                OriginalUrl = validatedUrl,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                _dbContext.Urls.Add(urlEntity);
                await _dbContext.SaveChangesAsync(cancellationToken);

                return new ShortUrlResponse
                {
                    Id = urlEntity.Id,
                    ShortCode = urlEntity.ShortCode,
                    OriginalUrl = urlEntity.OriginalUrl,
                    ShortUrl = BuildFullShortUrl(baseUrl, urlEntity.ShortCode),
                    CreatedAt = urlEntity.CreatedAt
                };
            }
            catch (DbUpdateException ex)
            {
                _logger.LogWarning(ex, "Database concurrency/unique index collision on short code '{Code}'. Retrying ({Attempt}/{MaxAttempts})...", shortCode, attempt, MaxGenerationAttempts);
                _dbContext.Entry(urlEntity).State = EntityState.Detached;
            }
        }

        throw new InvalidOperationException("Failed to generate a unique short URL code after multiple attempts. Please try again.");
    }

    /// <inheritdoc />
    public async Task<string?> GetOriginalUrlAsync(string shortCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            return null;
        }

        var url = await _dbContext.Urls
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken);

        return url?.OriginalUrl;
    }

    /// <inheritdoc />
    public async Task<ShortUrlResponse?> GetUrlDetailsAsync(string shortCode, string baseUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shortCode))
        {
            return null;
        }

        var url = await _dbContext.Urls
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.ShortCode == shortCode, cancellationToken);

        if (url == null)
        {
            return null;
        }

        return new ShortUrlResponse
        {
            Id = url.Id,
            ShortCode = url.ShortCode,
            OriginalUrl = url.OriginalUrl,
            ShortUrl = BuildFullShortUrl(baseUrl, url.ShortCode),
            CreatedAt = url.CreatedAt
        };
    }

    /// <summary>
    /// Validates that a string is a valid absolute URI with an HTTP or HTTPS scheme.
    /// </summary>
    private static string ValidateUrl(string rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new ArgumentException("The URL cannot be empty.", nameof(rawUrl));
        }

        var trimmedUrl = rawUrl.Trim();

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uriResult) ||
            (uriResult.Scheme != Uri.UriSchemeHttp && uriResult.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("The provided URL is invalid. It must be a valid absolute URL with an HTTP or HTTPS scheme (e.g., https://example.com).", nameof(rawUrl));
        }

        return uriResult.AbsoluteUri;
    }

    /// <summary>
    /// Generates a cryptographically secure random Base62 short code.
    /// </summary>
    private static string GenerateSecureCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var i = 0; i < length; i++)
        {
            var randomIndex = RandomNumberGenerator.GetInt32(Base62Alphabet.Length);
            chars[i] = Base62Alphabet[randomIndex];
        }

        return new string(chars);
    }

    /// <summary>
    /// Combines the base URL and short code into a complete short URL.
    /// </summary>
    private static string BuildFullShortUrl(string baseUrl, string shortCode)
    {
        return $"{baseUrl.TrimEnd('/')}/{shortCode}";
    }
}
