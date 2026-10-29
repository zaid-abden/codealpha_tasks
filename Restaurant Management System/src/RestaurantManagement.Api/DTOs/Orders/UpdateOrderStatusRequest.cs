using System.ComponentModel.DataAnnotations;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.DTOs.Orders;

public class UpdateOrderStatusRequest
{
    [Required]
    public OrderStatus Status { get; set; }
}
