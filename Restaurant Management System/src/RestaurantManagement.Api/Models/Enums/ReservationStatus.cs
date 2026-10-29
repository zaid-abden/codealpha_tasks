using System.Text.Json.Serialization;

namespace RestaurantManagement.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ReservationStatus
{
    Pending = 1,
    Confirmed = 2,
    Seated = 3,
    Completed = 4,
    Cancelled = 5
}
