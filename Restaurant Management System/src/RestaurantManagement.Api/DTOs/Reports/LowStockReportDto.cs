namespace RestaurantManagement.Api.DTOs.Reports;

public class LowStockReportDto
{
    public int InventoryItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public decimal CurrentQuantity { get; set; }
    public decimal ReorderThreshold { get; set; }
    public decimal Deficit => Math.Max(0, ReorderThreshold - CurrentQuantity);
}
