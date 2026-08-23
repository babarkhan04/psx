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
        var last = await db.CashMovements.AsNoTracking()
            .Where(m => m.ShiftId == shiftId)
            .OrderByDescending(m => m.OccurredOn)
            .ThenByDescending(m => m.CreatedOn)
            .Select(m => (decimal?)m.BalanceAfter)
            .FirstOrDefaultAsync(ct);

        return last ?? 0m;
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
