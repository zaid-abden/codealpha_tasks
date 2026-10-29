using System.ComponentModel.DataAnnotations;

namespace EventRegistration.Api.Dtos.Registrations;

public class CreateRegistrationRequest
{
    [Required(ErrorMessage = "Participant name is required.")]
    [StringLength(150, MinimumLength = 1, ErrorMessage = "Participant name must be between 1 and 150 characters.")]
    public string ParticipantName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "A valid email address is required.")]
    [StringLength(256, ErrorMessage = "Email cannot exceed 256 characters.")]
    public string Email { get; set; } = string.Empty;
}

public class RegistrationResponse
{
    public int Id { get; init; }
    public int EventId { get; init; }
    public string ParticipantName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public DateTime RegisteredAt { get; init; }
    public string? EventTitle { get; init; }
}
