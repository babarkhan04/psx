using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;

namespace SmartPOS.Infrastructure.Services.Management;

internal static class CashLedgerHelper
{
    public static async Task<decimal> GetBalanceAsync(AppDbContext db, Guid shiftId, CancellationToken ct)
    {
        var persisted = await db.CashMovements.AsNoTracking()
            .Where(movement => movement.ShiftId == shiftId)
            .SumAsync(movement => (decimal?)movement.Amount, ct) ?? 0m;
        var pending = db.ChangeTracker.Entries<CashMovement>()
            .Where(entry => entry.State == EntityState.Added && entry.Entity.ShiftId == shiftId)
            .Sum(entry => entry.Entity.Amount);
        return persisted + pending;
    }

    public static async Task<Result> PostMovementAsync(
        AppDbContext db,
        Guid shiftId,
        Guid storeId,
        CashMovementType movementType,
        decimal amount,
        string? reference,
        string? notes,
        Guid? saleId,
        Guid? saleReturnId,
        DateTime occurredOn,
        CancellationToken ct)
    {
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.Status == ShiftStatus.Open, ct);
        if (shift is null)
        {
            return Result.Failure("No open shift found for this register.", "NO_SHIFT");
        }

        if (shift.StoreId != storeId)
        {
            return Result.Failure("Shift does not belong to this store.", "INVALID_SHIFT");
        }

        var balanceAfter = await GetBalanceAsync(db, shiftId, ct) + amount;
        db.CashMovements.Add(new CashMovement
        {
            ShiftId = shiftId,
            StoreId = storeId,
            MovementType = movementType,
            Amount = amount,
            BalanceAfter = balanceAfter,
            Reference = reference,
            Notes = notes,
            OccurredOn = occurredOn,
            SaleId = saleId,
            SaleReturnId = saleReturnId
        });

        return Result.Success();
    }
}
