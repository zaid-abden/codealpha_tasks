using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.DTOs.Applications;

public class ApplicationResponseDto
{
    public int Id { get; set; }
    public int JobId { get; set; }
    public string ApplicantName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CoverLetter { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTime AppliedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
