using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Inventory;
using RestaurantManagement.Api.DTOs.Menu;
using RestaurantManagement.Api.DTOs.Orders;
using RestaurantManagement.Api.DTOs.Reports;
using RestaurantManagement.Api.DTOs.Reservations;
using RestaurantManagement.Api.DTOs.Tables;
using RestaurantManagement.Api.Models.Enums;
using Xunit;

namespace RestaurantManagement.Tests.IntegrationTests;

public class ApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task MenuEndpoints_CreateAndRetrieve_ReturnsCreatedAndOk()
    {
        var createRequest = new CreateMenuItemRequest
        {
            Name = "Signature Lasagna",
            Description = "Layers of pasta, meat sauce, and bechamel",
            Category = "Pasta",
            Price = 18.50m,
            IsAvailable = true
        };

        var postResponse = await _client.PostAsJsonAsync("/api/menu", createRequest);
        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        var created = await postResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(created);
        Assert.Equal("Signature Lasagna", created.Name);

        var getResponse = await _client.GetAsync($"/api/menu/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var retrieved = await getResponse.Content.ReadFromJsonAsync<MenuItemDto>();
        Assert.NotNull(retrieved);
        Assert.Equal(18.50m, retrieved.Price);
    }

    [Fact]
    public async Task CreateTable_DuplicateTableNumber_Returns409ConflictWithProblemDetails()
    {
        var req = new CreateTableRequest { TableNumber = 55, Capacity = 4 };

        var resp1 = await _client.PostAsJsonAsync("/api/tables", req);
        Assert.Equal(HttpStatusCode.Created, resp1.StatusCode);

        var resp2 = await _client.PostAsJsonAsync("/api/tables", req);
        Assert.Equal(HttpStatusCode.Conflict, resp2.StatusCode);

        var problem = await resp2.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("already exists", problem.Detail);
    }

    [Fact]
    public async Task CreateReservation_WhenConflicting_Returns409ConflictWithProblemDetails()
    {
        // 1. Create table
        var tableResp = await _client.PostAsJsonAsync("/api/tables", new CreateTableRequest { TableNumber = 60, Capacity = 4 });
        var table = await tableResp.Content.ReadFromJsonAsync<RestaurantTableDto>();

        var start = DateTime.UtcNow.AddDays(3).Date.AddHours(18);

        // 2. Book table 60 from 18:00 to 20:00
        var res1Resp = await _client.PostAsJsonAsync("/api/reservations", new CreateReservationRequest
        {
            CustomerName = "Alice",
            Email = "alice@test.com",
            PartySize = 4,
            TableId = table!.Id,
            StartAtUtc = start,
            EndAtUtc = start.AddHours(2)
        });
        Assert.Equal(HttpStatusCode.Created, res1Resp.StatusCode);

        // 3. Book same table from 19:00 to 21:00 (overlap)
        var res2Resp = await _client.PostAsJsonAsync("/api/reservations", new CreateReservationRequest
        {
            CustomerName = "Bob",
            Email = "bob@test.com",
            PartySize = 4,
            TableId = table.Id,
            StartAtUtc = start.AddHours(1),
            EndAtUtc = start.AddHours(3)
        });

        Assert.Equal(HttpStatusCode.Conflict, res2Resp.StatusCode);
        var problem = await res2Resp.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(409, problem.Status);
        Assert.Contains("already reserved", problem.Detail);
    }

    [Fact]
    public async Task CreateOrder_ServerCalculatesTotalsAndIgnoresClientPrices()
    {
        // 1. Create menu item with DB price $15.00
        var menuResp = await _client.PostAsJsonAsync("/api/menu", new CreateMenuItemRequest
        {
            Name = "Ribeye Steak",
            Category = "Steaks",
            Price = 15.00m,
            IsAvailable = true
        });
        var menuItem = await menuResp.Content.ReadFromJsonAsync<MenuItemDto>();

        // 2. Client submits order with quantity 3 (cannot specify price in request)
        var orderResp = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = menuItem!.Id, Quantity = 3 }
            }
        });

        Assert.Equal(HttpStatusCode.Created, orderResp.StatusCode);
        var order = await orderResp.Content.ReadFromJsonAsync<OrderDto>();
        Assert.NotNull(order);
        Assert.Equal(45.00m, order.Subtotal);
        Assert.Equal(45.00m, order.Total);
        Assert.Equal(OrderStatus.Pending, order.Status);
    }

    [Fact]
    public async Task InventoryAutoDeduction_Workflow_EndToEnd()
    {
        // 1. Create inventory item (e.g. Coffee Beans 500g)
        var invResp = await _client.PostAsJsonAsync("/api/inventory", new CreateInventoryItemRequest
        {
            Name = "Espresso Beans",
            Unit = "g",
            CurrentQuantity = 500m,
            ReorderThreshold = 100m
        });
        var invItem = await invResp.Content.ReadFromJsonAsync<InventoryItemDto>();

        // 2. Create menu item (Espresso) with recipe: 20g beans per cup
        var menuResp = await _client.PostAsJsonAsync("/api/menu", new CreateMenuItemRequest
        {
            Name = "Double Espresso",
            Category = "Beverages",
            Price = 4.00m,
            IsAvailable = true,
            Ingredients = new List<SetMenuItemIngredientRequest>
            {
                new() { InventoryItemId = invItem!.Id, QuantityRequired = 20m }
            }
        });
        var menuItem = await menuResp.Content.ReadFromJsonAsync<MenuItemDto>();

        // 3. Create order for 5 Espressos (5 * 20g = 100g needed)
        var orderResp = await _client.PostAsJsonAsync("/api/orders", new CreateOrderRequest
        {
            OrderType = OrderType.Takeaway,
            Items = new List<CreateOrderItemRequest>
            {
                new() { MenuItemId = menuItem!.Id, Quantity = 5 }
            }
        });
        var order = await orderResp.Content.ReadFromJsonAsync<OrderDto>();

        // 4. Update order status to Preparing (triggers auto-deduction)
        var statusResp = await _client.PatchAsJsonAsync($"/api/orders/{order!.Id}/status", new UpdateOrderStatusRequest
        {
            Status = OrderStatus.Preparing
        });
        Assert.Equal(HttpStatusCode.OK, statusResp.StatusCode);

        // 5. Verify inventory item was deducted (500 - 100 = 400g)
        var refreshedInvResp = await _client.GetAsync($"/api/inventory/{invItem.Id}");
        var refreshedInv = await refreshedInvResp.Content.ReadFromJsonAsync<InventoryItemDto>();
        Assert.Equal(400m, refreshedInv!.CurrentQuantity);

        // 6. Verify audit movements endpoint
        var movementsResp = await _client.GetAsync($"/api/inventory/{invItem.Id}/movements");
        var movements = await movementsResp.Content.ReadFromJsonAsync<List<InventoryMovementDto>>();
        Assert.NotNull(movements);
        Assert.Contains(movements, m => m.OrderId == order.Id && m.QuantityChange == -100m);
    }

    [Fact]
    public async Task ReportsEndpoints_DailySalesAndLowStock_ReturnValidData()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var salesResp = await _client.GetAsync($"/api/reports/daily-sales?date={today}");
        Assert.Equal(HttpStatusCode.OK, salesResp.StatusCode);
        var salesReport = await salesResp.Content.ReadFromJsonAsync<DailySalesReportDto>();
        Assert.NotNull(salesReport);

        var lowStockResp = await _client.GetAsync("/api/reports/low-stock");
        Assert.Equal(HttpStatusCode.OK, lowStockResp.StatusCode);
        var lowStockReport = await lowStockResp.Content.ReadFromJsonAsync<List<LowStockReportDto>>();
        Assert.NotNull(lowStockReport);
    }
}
