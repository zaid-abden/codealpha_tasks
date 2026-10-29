namespace RestaurantManagement.Api.Models;

public class MenuItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public ICollection<MenuItemIngredient> Ingredients { get; set; } = new List<MenuItemIngredient>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
