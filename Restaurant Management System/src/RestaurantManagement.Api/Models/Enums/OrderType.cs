using System.Text.Json.Serialization;

namespace RestaurantManagement.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum OrderType
{
    DineIn = 1,
    Takeaway = 2
}
