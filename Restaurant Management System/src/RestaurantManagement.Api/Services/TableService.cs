using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Tables;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Services;

public class TableService : ITableService
{
    private readonly RestaurantDbContext _context;

    public TableService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<List<RestaurantTableDto>> GetTablesAsync(TableFilterParams filter)
    {
        var query = _context.RestaurantTables.AsNoTracking().AsQueryable();

        if (filter.MinCapacity.HasValue)
        {
            query = query.Where(t => t.Capacity >= filter.MinCapacity.Value);
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(t => t.IsActive == filter.IsActive.Value);
        }

        return await query
            .OrderBy(t => t.TableNumber)
            .Select(t => new RestaurantTableDto
            {
                Id = t.Id,
                TableNumber = t.TableNumber,
                Capacity = t.Capacity,
                IsActive = t.IsActive
            })
            .ToListAsync();
    }

    public async Task<RestaurantTableDto> GetTableByIdAsync(int id)
    {
        var table = await _context.RestaurantTables
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id);

        if (table == null)
        {
            throw new NotFoundException($"Table with ID {id} was not found.");
        }

        return new RestaurantTableDto
        {
            Id = table.Id,
            TableNumber = table.TableNumber,
            Capacity = table.Capacity,
            IsActive = table.IsActive
        };
    }

    public async Task<RestaurantTableDto> CreateTableAsync(CreateTableRequest request)
    {
        var exists = await _context.RestaurantTables
            .AnyAsync(t => t.TableNumber == request.TableNumber);

        if (exists)
        {
            throw new ConflictException($"Table number {request.TableNumber} already exists.");
        }

        var table = new RestaurantTable
        {
            TableNumber = request.TableNumber,
            Capacity = request.Capacity,
            IsActive = true
        };

        _context.RestaurantTables.Add(table);
        await _context.SaveChangesAsync();

        return new RestaurantTableDto
        {
            Id = table.Id,
            TableNumber = table.TableNumber,
            Capacity = table.Capacity,
            IsActive = table.IsActive
        };
    }

    public async Task<RestaurantTableDto> UpdateTableAsync(int id, UpdateTableRequest request)
    {
        var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.Id == id);

        if (table == null)
        {
            throw new NotFoundException($"Table with ID {id} was not found.");
        }

        // Check duplicate table number
        var duplicateNumber = await _context.RestaurantTables
            .AnyAsync(t => t.TableNumber == request.TableNumber && t.Id != id);

        if (duplicateNumber)
        {
            throw new ConflictException($"Another table with number {request.TableNumber} already exists.");
        }

        // Policy: Prevent deactivation if doing so would violate active reservation policy
        if (table.IsActive && !request.IsActive)
        {
            var nowUtc = DateTime.UtcNow;
            var hasActiveFutureReservations = await _context.Reservations
                .AnyAsync(r => r.TableId == id
                    && r.EndAtUtc > nowUtc
                    && (r.Status == ReservationStatus.Pending ||
                        r.Status == ReservationStatus.Confirmed ||
                        r.Status == ReservationStatus.Seated));

            if (hasActiveFutureReservations)
            {
                throw new ConflictException(
                    $"Cannot deactivate Table {table.TableNumber} because it currently has active upcoming reservations.");
            }
        }

        table.TableNumber = request.TableNumber;
        table.Capacity = request.Capacity;
        table.IsActive = request.IsActive;

        await _context.SaveChangesAsync();

        return new RestaurantTableDto
        {
            Id = table.Id,
            TableNumber = table.TableNumber,
            Capacity = table.Capacity,
            IsActive = table.IsActive
        };
    }
}
