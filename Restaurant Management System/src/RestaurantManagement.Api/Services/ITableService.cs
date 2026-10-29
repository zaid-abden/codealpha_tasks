using RestaurantManagement.Api.DTOs.Tables;

namespace RestaurantManagement.Api.Services;

public interface ITableService
{
    Task<List<RestaurantTableDto>> GetTablesAsync(TableFilterParams filter);
    Task<RestaurantTableDto> GetTableByIdAsync(int id);
    Task<RestaurantTableDto> CreateTableAsync(CreateTableRequest request);
    Task<RestaurantTableDto> UpdateTableAsync(int id, UpdateTableRequest request);
}
