using System.Text.Json.Serialization;

namespace RestaurantManagement.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderStatus
{
    Pending = 1,
    Preparing = 2,
    Ready = 3,
    Served = 4,
    Completed = 5,
    Cancelled = 6
}
