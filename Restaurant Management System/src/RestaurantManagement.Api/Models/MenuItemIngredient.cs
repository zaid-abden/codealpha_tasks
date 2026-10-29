namespace RestaurantManagement.Api.Models;

public class MenuItemIngredient
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;
    public int InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public decimal QuantityRequired { get; set; }
}
