using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RestaurantManagement.Api.Data;
using RestaurantManagement.Api.DTOs.Common;
using RestaurantManagement.Api.DTOs.Reservations;
using RestaurantManagement.Api.Exceptions;
using RestaurantManagement.Api.Models;
using RestaurantManagement.Api.Models.Enums;

namespace RestaurantManagement.Api.Services;

public class ReservationService : IReservationService
{
    private readonly RestaurantDbContext _context;

    public ReservationService(RestaurantDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<ReservationDto>> GetReservationsAsync(ReservationFilterParams filter)
    {
        var query = _context.Reservations
            .AsNoTracking()
            .Include(r => r.Table)
            .AsQueryable();

        if (filter.Date.HasValue)
        {
            var date = filter.Date.Value.Date;
            var nextDate = date.AddDays(1);
            query = query.Where(r => r.StartAtUtc >= date && r.StartAtUtc < nextDate);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.TableId.HasValue)
        {
            query = query.Where(r => r.TableId == filter.TableId.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(r => r.StartAtUtc)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(r => MapToDto(r))
            .ToListAsync();

        return new PagedResult<ReservationDto>(items, totalCount, filter.PageNumber, filter.PageSize);
    }

    public async Task<ReservationDto> GetReservationByIdAsync(int id)
    {
        var reservation = await _context.Reservations
            .AsNoTracking()
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            throw new NotFoundException($"Reservation with ID {id} was not found.");
        }

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CreateReservationAsync(CreateReservationRequest request)
    {
        // 1. Calculate and validate times
        var startUtc = request.StartAtUtc.ToUniversalTime();
        var endUtc = request.EndAtUtc.HasValue
            ? request.EndAtUtc.Value.ToUniversalTime()
            : startUtc.AddMinutes(request.DurationMinutes ?? 120);

        if (endUtc <= startUtc)
        {
            throw new BadRequestException("Reservation end time must be after start time.");
        }

        // Reservation must be in future (allow a small clock skew of 1 minute)
        if (startUtc < DateTime.UtcNow.AddMinutes(-1))
        {
            throw new BadRequestException("Reservation start time must be in the future.");
        }

        IDbContextTransaction? transaction = null;
        if (_context.Database.IsRelational())
        {
            // Use Serializable transaction isolation in SQL Server to prevent concurrent double-booking
            transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        }

        try
        {
            int assignedTableId;

            if (request.TableId.HasValue)
            {
                // Specific table requested
                var table = await _context.RestaurantTables
                    .FirstOrDefaultAsync(t => t.Id == request.TableId.Value);

                if (table == null)
                {
                    throw new NotFoundException($"Table with ID {request.TableId.Value} was not found.");
                }

                if (!table.IsActive)
                {
                    throw new ConflictException($"Table {table.TableNumber} is currently inactive.");
                }

                if (table.Capacity < request.PartySize)
                {
                    throw new ConflictException(
                        $"Table {table.TableNumber} has capacity {table.Capacity}, which is insufficient for party size {request.PartySize}.");
                }

                // Check overlap
                var hasConflict = await HasOverlappingReservationAsync(table.Id, startUtc, endUtc);
                if (hasConflict)
                {
                    throw new ConflictException($"Table {table.TableNumber} is already reserved for the requested time interval.");
                }

                assignedTableId = table.Id;
            }
            else
            {
                // Auto-assignment: Find active tables with capacity >= party size
                var candidateTables = await _context.RestaurantTables
                    .Where(t => t.IsActive && t.Capacity >= request.PartySize)
                    .OrderBy(t => t.Capacity)
                    .ThenBy(t => t.TableNumber)
                    .ToListAsync();

                if (!candidateTables.Any())
                {
                    throw new ConflictException($"No table with capacity >= {request.PartySize} exists in the restaurant.");
                }

                RestaurantTable? availableTable = null;
                foreach (var candidate in candidateTables)
                {
                    var hasConflict = await HasOverlappingReservationAsync(candidate.Id, startUtc, endUtc);
                    if (!hasConflict)
                    {
                        availableTable = candidate;
                        break;
                    }
                }

                if (availableTable == null)
                {
                    throw new ConflictException("No suitable table is available for the requested time interval and party size.");
                }

                assignedTableId = availableTable.Id;
            }

            var reservation = new Reservation
            {
                CustomerName = request.CustomerName.Trim(),
                Email = request.Email?.Trim(),
                Phone = request.Phone?.Trim(),
                PartySize = request.PartySize,
                TableId = assignedTableId,
                StartAtUtc = startUtc,
                EndAtUtc = endUtc,
                Status = ReservationStatus.Confirmed, // Direct confirmed upon booking or pending
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            if (transaction != null)
            {
                await transaction.CommitAsync();
            }

            return await GetReservationByIdAsync(reservation.Id);
        }
        catch
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
            }
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    public async Task<ReservationDto> UpdateStatusAsync(int id, UpdateReservationStatusRequest request)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            throw new NotFoundException($"Reservation with ID {id} was not found.");
        }

        var currentStatus = reservation.Status;
        var newStatus = request.Status;

        if (currentStatus == newStatus)
        {
            return MapToDto(reservation);
        }

        // Validate state transitions:
        // Pending -> Confirmed, Cancelled
        // Confirmed -> Seated, Cancelled
        // Seated -> Completed, Cancelled
        // Completed -> None
        // Cancelled -> None
        bool isValidTransition = currentStatus switch
        {
            ReservationStatus.Pending => newStatus is ReservationStatus.Confirmed or ReservationStatus.Cancelled,
            ReservationStatus.Confirmed => newStatus is ReservationStatus.Seated or ReservationStatus.Cancelled,
            ReservationStatus.Seated => newStatus is ReservationStatus.Completed or ReservationStatus.Cancelled,
            ReservationStatus.Completed => false,
            ReservationStatus.Cancelled => false,
            _ => false
        };

        if (!isValidTransition)
        {
            throw new ConflictException(
                $"Invalid status transition from '{currentStatus}' to '{newStatus}'.");
        }

        reservation.Status = newStatus;
        await _context.SaveChangesAsync();

        return MapToDto(reservation);
    }

    public async Task<ReservationDto> CancelReservationAsync(int id)
    {
        var reservation = await _context.Reservations
            .Include(r => r.Table)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (reservation == null)
        {
            throw new NotFoundException($"Reservation with ID {id} was not found.");
        }

        if (reservation.Status == ReservationStatus.Cancelled)
        {
            return MapToDto(reservation);
        }

        if (reservation.Status == ReservationStatus.Completed)
        {
            throw new ConflictException("Completed reservations cannot be cancelled.");
        }

        reservation.Status = ReservationStatus.Cancelled;
        await _context.SaveChangesAsync();

        return MapToDto(reservation);
    }

    private async Task<bool> HasOverlappingReservationAsync(int tableId, DateTime startUtc, DateTime endUtc)
    {
        // Half-open interval overlap: existing.StartAtUtc < requested.EndAtUtc && existing.EndAtUtc > requested.StartAtUtc
        // Exclude cancelled reservations
        return await _context.Reservations.AnyAsync(r =>
            r.TableId == tableId &&
            r.Status != ReservationStatus.Cancelled &&
            r.StartAtUtc < endUtc &&
            r.EndAtUtc > startUtc);
    }

    private static ReservationDto MapToDto(Reservation r)
    {
        return new ReservationDto
        {
            Id = r.Id,
            CustomerName = r.CustomerName,
            Email = r.Email,
            Phone = r.Phone,
            PartySize = r.PartySize,
            TableId = r.TableId,
            TableNumber = r.Table?.TableNumber ?? 0,
            StartAtUtc = r.StartAtUtc,
            EndAtUtc = r.EndAtUtc,
            Status = r.Status,
            CreatedAtUtc = r.CreatedAtUtc
        };
    }
}
