namespace RestaurantManagement.Api.DTOs.Menu;

public class MenuItemIngredientDto
{
    public int Id { get; set; }
    public int InventoryItemId { get; set; }
    public string InventoryItemName { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal QuantityRequired { get; set; }
}
