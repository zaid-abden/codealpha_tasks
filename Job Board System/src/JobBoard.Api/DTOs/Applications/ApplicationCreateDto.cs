using System.ComponentModel.DataAnnotations;

namespace JobBoard.Api.DTOs.Applications;

public class ApplicationCreateDto
{
    [Required(ErrorMessage = "Applicant name is required.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Applicant name must be between 2 and 150 characters.")]
    public string ApplicantName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "Cover letter must not exceed 4000 characters.")]
    public string? CoverLetter { get; set; }
}
