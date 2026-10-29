using System.ComponentModel.DataAnnotations;

namespace RestaurantManagement.Api.DTOs.Reservations;

public class CreateReservationRequest : IValidatableObject
{
    [Required]
    [StringLength(120, MinimumLength = 1)]
    public string CustomerName { get; set; } = string.Empty;

    [EmailAddress]
    [StringLength(150)]
    public string? Email { get; set; }

    [Phone]
    [StringLength(30)]
    public string? Phone { get; set; }

    [Required]
    [Range(1, 100, ErrorMessage = "Party size must be between 1 and 100.")]
    public int PartySize { get; set; }

    public int? TableId { get; set; }

    [Required]
    public DateTime StartAtUtc { get; set; }

    public DateTime? EndAtUtc { get; set; }

    [Range(15, 480, ErrorMessage = "Duration must be between 15 and 480 minutes.")]
    public int? DurationMinutes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Email) && string.IsNullOrWhiteSpace(Phone))
        {
            yield return new ValidationResult(
                "At least one contact method (Email or Phone) must be provided.",
                new[] { nameof(Email), nameof(Phone) });
        }

        if (EndAtUtc == null && DurationMinutes == null)
        {
            yield return new ValidationResult(
                "Either EndAtUtc or DurationMinutes must be specified.",
                new[] { nameof(EndAtUtc), nameof(DurationMinutes) });
        }

        if (EndAtUtc.HasValue && EndAtUtc.Value <= StartAtUtc)
        {
            yield return new ValidationResult(
                "EndAtUtc must be later than StartAtUtc.",
                new[] { nameof(EndAtUtc) });
        }
    }
}
