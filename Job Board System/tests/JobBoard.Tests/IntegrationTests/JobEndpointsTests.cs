using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace JobBoard.Tests.IntegrationTests;

public class JobEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public JobEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task CreateJob_WithValidData_Returns201Created_AndDraftStatus()
    {
        var request = new JobCreateDto
        {
            Title = "Senior C# Engineer",
            Description = "We need an engineer experienced with .NET 10 and microservices.",
            CompanyName = "Enterprise Systems",
            Location = "Cairo, Egypt",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            MinimumSalary = 35000,
            MaximumSalary = 50000,
            CurrencyCode = "EGP",
            ApplicationDeadlineUtc = DateTime.UtcNow.AddMonths(1)
        };

        var response = await _client.PostAsJsonAsync("/api/jobs", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(JobStatus.Draft, created.Status);
        Assert.Equal("Senior C# Engineer", created.Title);
    }

    [Fact]
    public async Task CreateJob_WithInvalidSalary_Returns400BadRequest_ProblemDetails()
    {
        var request = new JobCreateDto
        {
            Title = "Invalid Salary Job",
            Description = "A job with bad salary values.",
            CompanyName = "Bad Pay Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            MinimumSalary = 60000,
            MaximumSalary = 30000 // Less than min
        };

        var response = await _client.PostAsJsonAsync("/api/jobs", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("salary", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishJob_WhenDraft_Returns200Ok_AndCanBeRetrievedPublicly()
    {
        // 1. Create a draft job
        var createRequest = new JobCreateDto
        {
            Title = "DevOps Specialist",
            Description = "Kubernetes and Azure pipelines expert.",
            CompanyName = "DevOps Hub",
            Location = "Alexandria",
            EmploymentType = EmploymentType.Contract,
            WorkArrangement = WorkArrangement.Remote,
            MinimumSalary = 25000,
            MaximumSalary = 35000,
            ApplicationDeadlineUtc = DateTime.UtcNow.AddDays(15)
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        // Verify it is NOT publicly accessible before publish
        var publicBefore = await _client.GetAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, publicBefore.StatusCode);

        // 2. Publish the job
        var publishResponse = await _client.PatchAsync($"/api/jobs/{created.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        var published = await publishResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(published);
        Assert.Equal(JobStatus.Published, published.Status);

        // 3. Verify it is now publicly accessible
        var publicAfter = await _client.GetAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, publicAfter.StatusCode);
        var retrieved = await publicAfter.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(retrieved);
        Assert.Equal(JobStatus.Published, retrieved.Status);
    }

    [Fact]
    public async Task PublishJob_WhenAlreadyPublished_Returns409Conflict()
    {
        var createRequest = new JobCreateDto
        {
            Title = "Frontend Lead",
            Description = "React and TypeScript architect.",
            CompanyName = "UI Tech",
            Location = "Giza",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.OnSite
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        // Publish once
        await _client.PatchAsync($"/api/jobs/{created.Id}/publish", null);

        // Publish second time
        var secondPublish = await _client.PatchAsync($"/api/jobs/{created.Id}/publish", null);
        Assert.Equal(HttpStatusCode.Conflict, secondPublish.StatusCode);
    }

    [Fact]
    public async Task CloseJob_WhenPublished_TransitionsToClosed_AndReturns200Ok()
    {
        var createRequest = new JobCreateDto
        {
            Title = "Product Manager",
            Description = "Drive product roadmap and vision.",
            CompanyName = "Product Co",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        // Publish
        await _client.PatchAsync($"/api/jobs/{created.Id}/publish", null);

        // Close
        var closeResponse = await _client.PatchAsync($"/api/jobs/{created.Id}/close", null);
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);

        var closed = await closeResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(closed);
        Assert.Equal(JobStatus.Closed, closed.Status);

        // Attempting to close again returns 409 Conflict
        var closeAgain = await _client.PatchAsync($"/api/jobs/{created.Id}/close", null);
        Assert.Equal(HttpStatusCode.Conflict, closeAgain.StatusCode);
    }

    [Fact]
    public async Task GetJobs_WithFiltersAndPagination_ReturnsMatchingPublishedJobs()
    {
        // Create and publish a specific searchable job
        var uniqueTitle = $"UniqueSearchKey_{Guid.NewGuid():N}";
        var createRequest = new JobCreateDto
        {
            Title = uniqueTitle,
            Description = "Matching description for keyword search testing.",
            CompanyName = "Searchable Company",
            Location = "Aswan",
            EmploymentType = EmploymentType.Freelance,
            WorkArrangement = WorkArrangement.Remote,
            MinimumSalary = 15000,
            MaximumSalary = 25000
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        await _client.PatchAsync($"/api/jobs/{created.Id}/publish", null);

        // Search by keyword
        var searchResponse = await _client.GetAsync($"/api/jobs?search={uniqueTitle}&location=Aswan&employmentType=Freelance&pageNumber=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        var pageResult = await searchResponse.Content.ReadFromJsonAsync<PagedResultDto<JobResponseDto>>(_jsonOptions);
        Assert.NotNull(pageResult);
        Assert.True(pageResult.TotalCount >= 1);
        Assert.Contains(pageResult.Items, j => j.Title == uniqueTitle);
    }

    [Fact]
    public async Task UpdateJob_WhenValid_UpdatesFieldsAndReturns200Ok()
    {
        var createRequest = new JobCreateDto
        {
            Title = "Junior Developer",
            Description = "Entry-level software engineer.",
            CompanyName = "Code Lab",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.OnSite
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        var updateRequest = new JobUpdateDto
        {
            Title = "Mid-Level Developer",
            Description = "Updated description with new requirements.",
            CompanyName = "Code Lab Advanced",
            Location = "New Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            MinimumSalary = 20000,
            MaximumSalary = 28000
        };

        var updateResponse = await _client.PutAsJsonAsync($"/api/jobs/{created.Id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Mid-Level Developer", updated.Title);
        Assert.Equal("New Cairo", updated.Location);
    }

    [Fact]
    public async Task DeleteJob_WhenJobHasNoApplications_Returns204NoContent()
    {
        var createRequest = new JobCreateDto
        {
            Title = "Temporary Job",
            Description = "Short-term temporary gig.",
            CompanyName = "Temp Co",
            Location = "Cairo",
            EmploymentType = EmploymentType.PartTime,
            WorkArrangement = WorkArrangement.OnSite
        };

        var createResponse = await _client.PostAsJsonAsync("/api/jobs", createRequest);
        var created = await createResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(created);

        var deleteResponse = await _client.DeleteAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await _client.GetAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }
}
