using JobBoard.Api.Models.Enums;

namespace JobBoard.Api.Models;

public class Application
{
    public int Id { get; set; }

    public int JobId { get; set; }

    public Job? Job { get; set; }

    public string ApplicantName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public string? CoverLetter { get; set; }

    public ApplicationStatus Status { get; set; } = ApplicationStatus.Submitted;

    public DateTime AppliedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }
}
