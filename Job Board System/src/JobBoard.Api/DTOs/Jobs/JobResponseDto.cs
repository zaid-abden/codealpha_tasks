using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.DTOs.Jobs;

public class JobResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public EmploymentType EmploymentType { get; set; }
    public WorkArrangement WorkArrangement { get; set; }
    public decimal? MinimumSalary { get; set; }
    public decimal? MaximumSalary { get; set; }
    public string CurrencyCode { get; set; } = "EGP";
    public JobStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public DateTime? ApplicationDeadlineUtc { get; set; }
    public bool IsAcceptingApplications =>
        Status == JobStatus.Published &&
        (!ApplicationDeadlineUtc.HasValue || ApplicationDeadlineUtc.Value > DateTime.UtcNow);
}
