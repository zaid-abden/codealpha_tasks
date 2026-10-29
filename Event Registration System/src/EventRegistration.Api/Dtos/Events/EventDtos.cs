using System.ComponentModel.DataAnnotations;
using EventRegistration.Api.Dtos.Common;

namespace EventRegistration.Api.Dtos.Events;

public class EventQueryParameters : PaginationQuery
{
    public string? Search { get; set; }
    public bool? UpcomingOnly { get; set; }
}

public class CreateEventRequest : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Location must be between 1 and 300 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "StartDateTime is required.")]
    public DateTime StartDateTime { get; set; }

    [Required(ErrorMessage = "EndDateTime is required.")]
    public DateTime EndDateTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
    public int Capacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDateTime <= StartDateTime)
        {
            yield return new ValidationResult(
                "End date/time must be strictly after start date/time.",
                new[] { nameof(EndDateTime) });
        }
    }
}

public class UpdateEventRequest : IValidatableObject
{
    [Required(ErrorMessage = "Title is required.")]
    [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Location is required.")]
    [StringLength(300, MinimumLength = 1, ErrorMessage = "Location must be between 1 and 300 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "StartDateTime is required.")]
    public DateTime StartDateTime { get; set; }

    [Required(ErrorMessage = "EndDateTime is required.")]
    public DateTime EndDateTime { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be greater than 0.")]
    public int Capacity { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (EndDateTime <= StartDateTime)
        {
            yield return new ValidationResult(
                "End date/time must be strictly after start date/time.",
                new[] { nameof(EndDateTime) });
        }
    }
}

public class EventResponse
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string Location { get; init; } = string.Empty;
    public DateTime StartDateTime { get; init; }
    public DateTime EndDateTime { get; init; }
    public int Capacity { get; init; }
    public int RegisteredCount { get; init; }
    public int RemainingSpots { get; init; }
    public DateTime CreatedAt { get; init; }
}
