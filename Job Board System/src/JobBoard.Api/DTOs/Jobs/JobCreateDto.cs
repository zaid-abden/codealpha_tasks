using System.ComponentModel.DataAnnotations;
using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.DTOs.Jobs;

public class JobCreateDto
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 4000 characters.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Company name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Company name must be between 2 and 150 characters.")]
    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Location must be between 2 and 150 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Employment type is required.")]
    [EnumDataType(typeof(EmploymentType), ErrorMessage = "Invalid employment type.")]
    public EmploymentType EmploymentType { get; set; }

    [Required(ErrorMessage = "Work arrangement is required.")]
    [EnumDataType(typeof(WorkArrangement), ErrorMessage = "Invalid work arrangement.")]
    public WorkArrangement WorkArrangement { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Minimum salary must be non-negative.")]
    public decimal? MinimumSalary { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "Maximum salary must be non-negative.")]
    public decimal? MaximumSalary { get; set; }

    [StringLength(3, MinimumLength = 3, ErrorMessage = "Currency code must be a 3-letter code.")]
    public string CurrencyCode { get; set; } = "EGP";

    public DateTime? ApplicationDeadlineUtc { get; set; }
}
