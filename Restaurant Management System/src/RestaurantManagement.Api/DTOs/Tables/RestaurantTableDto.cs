namespace RestaurantManagement.Api.DTOs.Tables;

public class RestaurantTableDto
{
    public int Id { get; set; }
    public int TableNumber { get; set; }
    public int Capacity { get; set; }
    public bool IsActive { get; set; }
}
