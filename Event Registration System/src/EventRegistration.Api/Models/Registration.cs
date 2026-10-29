namespace EventRegistration.Api.Models;

public class Registration
{
    public int Id { get; set; }

    public int EventId { get; set; }

    public string ParticipantName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string NormalizedEmail { get; set; } = string.Empty;

    public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

    public Event Event { get; set; } = null!;
}
