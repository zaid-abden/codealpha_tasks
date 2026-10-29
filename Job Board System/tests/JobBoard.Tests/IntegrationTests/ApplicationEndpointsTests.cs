using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Models.Enums;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace JobBoard.Tests.IntegrationTests;

public class ApplicationEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public ApplicationEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        _jsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    private async Task<JobResponseDto> CreateAndPublishJobAsync(string? title = null)
    {
        var request = new JobCreateDto
        {
            Title = title ?? $"Software Engineer {Guid.NewGuid():N}",
            Description = "Full stack developer wanted for modern web apps.",
            CompanyName = "Global Software House",
            Location = "Cairo, Egypt",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            MinimumSalary = 25000,
            MaximumSalary = 40000,
            ApplicationDeadlineUtc = DateTime.UtcNow.AddMonths(2)
        };

        var postResponse = await _client.PostAsJsonAsync("/api/jobs", request);
        var job = await postResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(job);

        var publishResponse = await _client.PatchAsync($"/api/jobs/{job.Id}/publish", null);
        Assert.Equal(HttpStatusCode.OK, publishResponse.StatusCode);

        return job;
    }

    [Fact]
    public async Task ApplyForJob_WithValidData_Returns201Created_AndApplicationDetails()
    {
        var job = await CreateAndPublishJobAsync();

        var request = new ApplicationCreateDto
        {
            ApplicantName = "Tamer Hosny",
            Email = "tamer.hosny@example.com",
            CoverLetter = "Extensive experience building scalable REST APIs."
        };

        var response = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(created);
        Assert.True(created.Id > 0);
        Assert.Equal(job.Id, created.JobId);
        Assert.Equal("Tamer Hosny", created.ApplicantName);
        Assert.Equal("tamer.hosny@example.com", created.Email);
        Assert.Equal(ApplicationStatus.Submitted, created.Status);
    }

    [Fact]
    public async Task ApplyForJob_WithDuplicateEmailCaseInsensitive_Returns409Conflict()
    {
        var job = await CreateAndPublishJobAsync();

        var firstRequest = new ApplicationCreateDto
        {
            ApplicantName = "Nouran Ali",
            Email = "nouran.ali@example.com",
            CoverLetter = "First application"
        };
        var firstResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", firstRequest);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        // Submit second application with uppercase and spaces in email
        var duplicateRequest = new ApplicationCreateDto
        {
            ApplicantName = "Nouran Ali",
            Email = "  NOURAN.ALI@EXAMPLE.COM  ",
            CoverLetter = "Duplicate application"
        };
        var secondResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", duplicateRequest);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var problem = await secondResponse.Content.ReadFromJsonAsync<ProblemDetails>(_jsonOptions);
        Assert.NotNull(problem);
        Assert.Contains("already been submitted", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApplyForJob_WhenJobIsDraft_Returns404NotFound()
    {
        // Create draft job without publishing
        var request = new JobCreateDto
        {
            Title = "Draft Only Job",
            Description = "Not yet published job listing.",
            CompanyName = "Draft Co",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote
        };
        var jobResponse = await _client.PostAsJsonAsync("/api/jobs", request);
        var job = await jobResponse.Content.ReadFromJsonAsync<JobResponseDto>(_jsonOptions);
        Assert.NotNull(job);

        var applyRequest = new ApplicationCreateDto
        {
            ApplicantName = "Omar Khaled",
            Email = "omar@example.com"
        };

        var response = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", applyRequest);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ApplyForJob_WhenJobIsClosed_Returns409Conflict()
    {
        var job = await CreateAndPublishJobAsync();

        // Close the job
        await _client.PatchAsync($"/api/jobs/{job.Id}/close", null);

        var applyRequest = new ApplicationCreateDto
        {
            ApplicantName = "Omar Khaled",
            Email = "omar@example.com"
        };

        var response = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", applyRequest);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetJobApplications_ReturnsPaginatedList()
    {
        var job = await CreateAndPublishJobAsync();

        // Submit multiple applications
        for (int i = 1; i <= 5; i++)
        {
            await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplicationCreateDto
            {
                ApplicantName = $"Applicant {i}",
                Email = $"app_{i}_{Guid.NewGuid():N}@example.com"
            });
        }

        var response = await _client.GetAsync($"/api/jobs/{job.Id}/applications?pageNumber=1&pageSize=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedResultDto<ApplicationResponseDto>>(_jsonOptions);
        Assert.NotNull(page);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.Items.Count);
        Assert.True(page.HasNextPage);
    }

    [Fact]
    public async Task UpdateApplicationStatus_WithValidTransition_Returns200Ok()
    {
        var job = await CreateAndPublishJobAsync();

        var applyResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplicationCreateDto
        {
            ApplicantName = "Status Test Applicant",
            Email = "status.test@example.com"
        });
        var app = await applyResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(app);

        // Transition: Submitted -> UnderReview
        var updateResponse = await _client.PatchAsJsonAsync($"/api/applications/{app.Id}/status", new ApplicationStatusUpdateDto
        {
            Status = ApplicationStatus.UnderReview
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updated = await updateResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(ApplicationStatus.UnderReview, updated.Status);
    }

    [Fact]
    public async Task UpdateApplicationStatus_WithInvalidTransition_Returns409Conflict()
    {
        var job = await CreateAndPublishJobAsync();

        var applyResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplicationCreateDto
        {
            ApplicantName = "Invalid Status Applicant",
            Email = "invalid.status@example.com"
        });
        var app = await applyResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(app);

        // Attempt invalid skip: Submitted -> Hired
        var updateResponse = await _client.PatchAsJsonAsync($"/api/applications/{app.Id}/status", new ApplicationStatusUpdateDto
        {
            Status = ApplicationStatus.Hired
        });
        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
    }

    [Fact]
    public async Task WithdrawApplication_TransitionsStatusToWithdrawn_AndCannotBeWithdrawnAgain()
    {
        var job = await CreateAndPublishJobAsync();

        var applyResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplicationCreateDto
        {
            ApplicantName = "Withdrawal Applicant",
            Email = "withdraw@example.com"
        });
        var app = await applyResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(app);

        // Withdraw application
        var withdrawResponse = await _client.DeleteAsync($"/api/applications/{app.Id}");
        Assert.Equal(HttpStatusCode.OK, withdrawResponse.StatusCode);

        var withdrawn = await withdrawResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(withdrawn);
        Assert.Equal(ApplicationStatus.Withdrawn, withdrawn.Status);

        // Attempting to withdraw again returns 409 Conflict
        var secondWithdraw = await _client.DeleteAsync($"/api/applications/{app.Id}");
        Assert.Equal(HttpStatusCode.Conflict, secondWithdraw.StatusCode);
    }

    [Fact]
    public async Task DeleteJob_WhenApplicationsExist_SoftDeletesJobAndPreservesApplications()
    {
        var job = await CreateAndPublishJobAsync();

        var applyResponse = await _client.PostAsJsonAsync($"/api/jobs/{job.Id}/applications", new ApplicationCreateDto
        {
            ApplicantName = "Preserve History Applicant",
            Email = "preserve@example.com"
        });
        var app = await applyResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(app);

        // Delete job that has applications
        var deleteResponse = await _client.DeleteAsync($"/api/jobs/{job.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        // Verify application still exists and can be retrieved
        var getAppResponse = await _client.GetAsync($"/api/applications/{app.Id}");
        Assert.Equal(HttpStatusCode.OK, getAppResponse.StatusCode);

        var existingApp = await getAppResponse.Content.ReadFromJsonAsync<ApplicationResponseDto>(_jsonOptions);
        Assert.NotNull(existingApp);
        Assert.Equal(app.Id, existingApp.Id);
    }
}
