using System.ComponentModel.DataAnnotations;
using JobBoard.Api.DTOs.Common;
using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.DTOs.Jobs;

public class JobFilterDto : PaginationQueryDto
{
    public string? Search { get; set; }
    public string? Location { get; set; }
    public EmploymentType? EmploymentType { get; set; }
    public WorkArrangement? WorkArrangement { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "MinSalary must be non-negative.")]
    public decimal? MinSalary { get; set; }

    [Range(0, 100_000_000, ErrorMessage = "MaxSalary must be non-negative.")]
    public decimal? MaxSalary { get; set; }

    /// <summary>
    /// Sorting option: "newest" (default), "salary_asc", "salary_desc", "title"
    /// </summary>
    public string? SortBy { get; set; } = "newest";
}
