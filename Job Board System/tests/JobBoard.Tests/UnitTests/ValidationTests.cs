using System.ComponentModel.DataAnnotations;
using JobBoard.Api.DTOs.Applications;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.DTOs.Jobs;
using JobBoard.Api.Models.Enums;
using Xunit;

namespace JobBoard.Tests.UnitTests;

public class ValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void JobCreateDto_WithValidData_PassesValidation()
    {
        var dto = new JobCreateDto
        {
            Title = "Senior .NET Developer",
            Description = "We are seeking an experienced .NET backend engineer.",
            CompanyName = "Tech Corp",
            Location = "Cairo, Egypt",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Hybrid,
            MinimumSalary = 30000,
            MaximumSalary = 45000,
            CurrencyCode = "EGP"
        };

        var errors = ValidateModel(dto);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "Description text here", "Tech Corp", "Cairo")]
    [InlineData("AB", "Description text here", "Tech Corp", "Cairo")] // Title too short
    [InlineData("Valid Title", "Short", "Tech Corp", "Cairo")] // Description too short (<10)
    [InlineData("Valid Title", "Valid description here", "", "Cairo")] // Missing company
    [InlineData("Valid Title", "Valid description here", "Tech Corp", "")] // Missing location
    public void JobCreateDto_WithInvalidRequiredFields_FailsValidation(
        string title, string description, string company, string location)
    {
        var dto = new JobCreateDto
        {
            Title = title,
            Description = description,
            CompanyName = company,
            Location = location,
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote
        };

        var errors = ValidateModel(dto);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void JobCreateDto_WithNegativeSalary_FailsValidation()
    {
        var dto = new JobCreateDto
        {
            Title = "Senior .NET Developer",
            Description = "We are looking for an experienced developer.",
            CompanyName = "Tech Corp",
            Location = "Cairo",
            EmploymentType = EmploymentType.FullTime,
            WorkArrangement = WorkArrangement.Remote,
            MinimumSalary = -1000
        };

        var errors = ValidateModel(dto);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(JobCreateDto.MinimumSalary)));
    }

    [Fact]
    public void ApplicationCreateDto_WithValidData_PassesValidation()
    {
        var dto = new ApplicationCreateDto
        {
            ApplicantName = "Ahmed Ali",
            Email = "ahmed.ali@example.com",
            CoverLetter = "I have 4 years of experience with C# and ASP.NET Core."
        };

        var errors = ValidateModel(dto);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData("", "test@example.com")] // Missing name
    [InlineData("A", "test@example.com")] // Name too short (<2)
    [InlineData("Ahmed Ali", "")] // Missing email
    [InlineData("Ahmed Ali", "invalid-email-format")] // Invalid email format
    public void ApplicationCreateDto_WithInvalidData_FailsValidation(string name, string email)
    {
        var dto = new ApplicationCreateDto
        {
            ApplicantName = name,
            Email = email
        };

        var errors = ValidateModel(dto);

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void PaginationQueryDto_EnforcesBounds()
    {
        var pagination = new PaginationQueryDto
        {
            PageNumber = -5,
            PageSize = 100
        };

        Assert.Equal(1, pagination.PageNumber);
        Assert.Equal(50, pagination.PageSize); // Capped at MaxPageSize 50
    }
}
