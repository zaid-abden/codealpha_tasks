using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using UrlShortener.Api.Data;
using UrlShortener.Api.DTOs;
using UrlShortener.Api.Models;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests;

/// <summary>
/// Unit tests for UrlShortenerService business logic using EF Core In-Memory provider.
/// Note: EF Core InMemory is used here to isolate tests from the external SQL Server database.
/// </summary>
public class UrlShortenerServiceTests
{
    private static ApplicationDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task CreateShortUrlAsync_WithValidUrl_CreatesAndReturnsShortUrl()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);
        var request = new CreateShortUrlRequest { LongUrl = "https://example.com/test-article" };
        var baseUrl = "https://localhost:7001";

        // Act
        var result = await service.CreateShortUrlAsync(request, baseUrl);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.ShortCode);
        Assert.Equal(7, result.ShortCode.Length);
        Assert.Equal("https://example.com/test-article", result.OriginalUrl);
        Assert.Equal($"https://localhost:7001/{result.ShortCode}", result.ShortUrl);
        Assert.True(result.CreatedAt <= DateTime.UtcNow);

        // Verify database persistence
        var stored = await dbContext.Urls.FirstOrDefaultAsync(u => u.ShortCode == result.ShortCode);
        Assert.NotNull(stored);
        Assert.Equal("https://example.com/test-article", stored.OriginalUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://ftp.example.com/file.zip")]
    [InlineData("javascript:alert(1)")]
    public async Task CreateShortUrlAsync_WithInvalidUrl_ThrowsArgumentException(string invalidUrl)
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);
        var request = new CreateShortUrlRequest { LongUrl = invalidUrl };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.CreateShortUrlAsync(request, "https://localhost:7001"));
    }

    [Fact]
    public async Task CreateShortUrlAsync_GeneratesUniqueCodesAcrossCalls()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);
        var baseUrl = "https://localhost:7001";
        var generatedCodes = new HashSet<string>();

        // Act - generate 25 codes sequentially
        for (var i = 0; i < 25; i++)
        {
            var request = new CreateShortUrlRequest { LongUrl = $"https://example.com/item/{i}" };
            var response = await service.CreateShortUrlAsync(request, baseUrl);
            generatedCodes.Add(response.ShortCode);
        }

        // Assert - all 25 codes should be unique
        Assert.Equal(25, generatedCodes.Count);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_WithExistingCode_ReturnsOriginalUrl()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var expectedOriginal = "https://example.com/my-target";
        var code = "abc1234";

        dbContext.Urls.Add(new Url
        {
            ShortCode = code,
            OriginalUrl = expectedOriginal,
            CreatedAt = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);

        // Act
        var result = await service.GetOriginalUrlAsync(code);

        // Assert
        Assert.Equal(expectedOriginal, result);
    }

    [Fact]
    public async Task GetOriginalUrlAsync_WithUnknownCode_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);

        // Act
        var result = await service.GetOriginalUrlAsync("nonExistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetUrlDetailsAsync_WithExistingCode_ReturnsFullDetails()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var code = "xyz9876";
        var originalUrl = "https://example.com/details-test";
        var createdAt = DateTime.UtcNow.AddMinutes(-5);

        dbContext.Urls.Add(new Url
        {
            ShortCode = code,
            OriginalUrl = originalUrl,
            CreatedAt = createdAt
        });
        await dbContext.SaveChangesAsync();

        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);

        // Act
        var result = await service.GetUrlDetailsAsync(code, "https://localhost:7001");

        // Assert
        Assert.NotNull(result);
        Assert.Equal(code, result.ShortCode);
        Assert.Equal(originalUrl, result.OriginalUrl);
        Assert.Equal($"https://localhost:7001/{code}", result.ShortUrl);
        Assert.Equal(createdAt, result.CreatedAt);
    }

    [Fact]
    public async Task GetUrlDetailsAsync_WithUnknownCode_ReturnsNull()
    {
        // Arrange
        using var dbContext = CreateInMemoryDbContext();
        var service = new UrlShortenerService(dbContext, NullLogger<UrlShortenerService>.Instance);

        // Act
        var result = await service.GetUrlDetailsAsync("unknownCode", "https://localhost:7001");

        // Assert
        Assert.Null(result);
    }
}
