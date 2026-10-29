using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Api.Data;
using UrlShortener.Api.DTOs;

namespace UrlShortener.Tests;

/// <summary>
/// End-to-end HTTP integration tests for UrlShortener API endpoints.
/// Uses WebApplicationFactory with an isolated in-memory database per test run.
///
/// NOTE ON TEST DATABASE PROVIDER:
/// These tests use the EF Core InMemory database provider to run completely isolated
/// from the live SQL Server development database. InMemory verifies routing, HTTP responses,
/// model binding, and service business logic. However, InMemory does not enforce SQL Server-specific
/// constraint checks (e.g. relational foreign keys, true transactional locking), so it is used
/// for automated testing rather than full database provider emulation.
/// </summary>
public class UrlsApiIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public UrlsApiIntegrationTests(WebApplicationFactory<Program> factory)
    {
        var inMemoryServiceProvider = new ServiceCollection()
            .AddEntityFrameworkInMemoryDatabase()
            .BuildServiceProvider();

        var dbName = "IntegrationTestDb_" + Guid.NewGuid();

        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                var descriptor = services.SingleOrDefault(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));

                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }

                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase(dbName)
                           .UseInternalServiceProvider(inMemoryServiceProvider);
                });
            });
        });
    }

    [Fact]
    public async Task PostApiUrls_WithValidPayload_Returns201CreatedAndShortUrl()
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new CreateShortUrlRequest
        {
            LongUrl = "https://example.com/internship-project/url-shortener"
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/urls", payload);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = await response.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(data);
        Assert.NotEmpty(data.ShortCode);
        Assert.Equal(payload.LongUrl, data.OriginalUrl);
        Assert.Contains(data.ShortCode, data.ShortUrl);
        Assert.NotNull(response.Headers.Location);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-url")]
    [InlineData("ftp://files.example.com")]
    public async Task PostApiUrls_WithInvalidUrl_Returns400BadRequest(string invalidUrl)
    {
        // Arrange
        var client = _factory.CreateClient();
        var payload = new CreateShortUrlRequest { LongUrl = invalidUrl };

        // Act
        var response = await client.PostAsJsonAsync("/api/urls", payload);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetApiUrlsShortCode_WithExistingCode_Returns200Ok()
    {
        // Arrange - first create a short URL
        var client = _factory.CreateClient();
        var createPayload = new CreateShortUrlRequest { LongUrl = "https://example.com/details-test" };
        var createResponse = await client.PostAsJsonAsync("/api/urls", createPayload);
        var created = await createResponse.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(created);

        // Act - retrieve its details
        var response = await client.GetAsync($"/api/urls/{created.ShortCode}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var details = await response.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(details);
        Assert.Equal(created.ShortCode, details.ShortCode);
        Assert.Equal(created.OriginalUrl, details.OriginalUrl);
    }

    [Fact]
    public async Task GetApiUrlsShortCode_WithUnknownCode_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/urls/nonExistentCode");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetShortCode_WithExistingCode_Returns302FoundAndRedirectsToOriginal()
    {
        // Arrange - disable auto-redirect to inspect the 302 response and Location header
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var targetUrl = "https://example.com/target-redirect-destination";
        var createResponse = await client.PostAsJsonAsync("/api/urls", new CreateShortUrlRequest
        {
            LongUrl = targetUrl
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ShortUrlResponse>();
        Assert.NotNull(created);

        // Act - hit the redirect endpoint /{shortCode}
        var redirectResponse = await client.GetAsync($"/{created.ShortCode}");

        // Assert - verify HTTP 302 Found and Location header
        Assert.Equal(HttpStatusCode.Redirect, redirectResponse.StatusCode);
        Assert.Equal(targetUrl, redirectResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task GetShortCode_WithUnknownCode_Returns404NotFound()
    {
        // Arrange
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Act
        var response = await client.GetAsync("/unknown123");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
