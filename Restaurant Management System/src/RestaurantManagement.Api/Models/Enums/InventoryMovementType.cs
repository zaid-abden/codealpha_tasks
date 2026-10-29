using System.Text.Json.Serialization;

namespace RestaurantManagement.Api.Models.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum InventoryMovementType
{
    OrderDeduction = 1,
    ManualAdjustment = 2,
    Restock = 3,
    InitialStock = 4
}
