using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class ShiftService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    INumberSequenceService numberSequence,
    DbContextGate gate) : IShiftService
{
    public Task<ShiftContextDto?> GetActiveShiftAsync(Guid registerId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetActiveShiftCoreAsync(registerId, ct), ct);

    public Task<IReadOnlyList<ShiftListItemDto>> GetShiftsAsync(ShiftQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetShiftsCoreAsync(query, ct), ct);

    public Task<IReadOnlyList<RegisterOptionDto>> GetRegisterOptionsAsync(Guid? storeId = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetRegisterOptionsCoreAsync(storeId, ct), ct);

    public Task<Result<Guid>> OpenShiftAsync(OpenShiftDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => OpenShiftCoreAsync(dto, ct), ct);

    public Task<Result> RecordCashMovementAsync(RecordCashMovementDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => RecordCashMovementCoreAsync(dto, ct), ct);

    public Task<Result> RecordNoSaleDrawerAsync(NoSaleDrawerDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => RecordNoSaleDrawerCoreAsync(dto, ct), ct);

    public Task<Result> CloseShiftAsync(CloseShiftDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CloseShiftCoreAsync(dto, ct), ct);

    public Task<ShiftReportDto?> GetReportAsync(Guid shiftId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetReportCoreAsync(shiftId, ct), ct);

    private async Task<ShiftContextDto?> GetActiveShiftCoreAsync(Guid registerId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking()
            .Include(s => s.Register)
            .FirstOrDefaultAsync(s => s.RegisterId == registerId && s.Status == ShiftStatus.Open, ct);

        if (shift is null)
        {
            return null;
        }

        var balance = await CashLedgerHelper.GetBalanceAsync(db, shift.Id, ct);
        return new ShiftContextDto
        {
            ShiftId = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            RegisterId = shift.RegisterId,
            RegisterName = shift.Register?.Name ?? "Register",
            OpeningFloat = shift.OpeningFloat,
            CurrentCashBalance = balance,
            OpenedOn = shift.OpenedOn
        };
    }

    private async Task<IReadOnlyList<ShiftListItemDto>> GetShiftsCoreAsync(ShiftQueryDto query, CancellationToken ct)
    {
        var q = db.Shifts.AsNoTracking()
            .Include(s => s.Register!).ThenInclude(r => r.Store)
            .AsQueryable();

        if (query.StoreId is { } storeId)
        {
            q = q.Where(s => s.StoreId == storeId);
        }

        if (query.RegisterId is { } registerId)
        {
            q = q.Where(s => s.RegisterId == registerId);
        }

        if (query.OpenOnly)
        {
            q = q.Where(s => s.Status == ShiftStatus.Open);
        }

        var shifts = await q.OrderByDescending(s => s.OpenedOn).Take(query.Take <= 0 ? 50 : query.Take).ToListAsync(ct);
        var userIds = shifts.SelectMany(s => new[] { s.OpenedByUserId, s.ClosedByUserId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName ?? "User", ct);

        return shifts.Select(s => new ShiftListItemDto
        {
            Id = s.Id,
            ShiftNumber = s.ShiftNumber,
            RegisterName = s.Register?.Name ?? "",
            StoreName = s.Register?.Store is { } store ? $"{store.Code} · {store.Name}" : "",
            OpenedByName = users.GetValueOrDefault(s.OpenedByUserId, "User"),
            ClosedByName = s.ClosedByUserId is null ? null : users.GetValueOrDefault(s.ClosedByUserId, "User"),
            Status = s.Status.ToString(),
            OpeningFloat = s.OpeningFloat,
            ExpectedCash = s.ExpectedCash,
            CountedCash = s.CountedCash,
            Variance = s.Variance,
            OpenedOn = s.OpenedOn,
            ClosedOn = s.ClosedOn
        }).ToList();
    }

    private async Task<IReadOnlyList<RegisterOptionDto>> GetRegisterOptionsCoreAsync(Guid? storeId, CancellationToken ct)
    {
        var query = db.Registers.AsNoTracking()
            .Include(r => r.Store)
            .Where(r => r.IsActive);

        if (storeId is { } sid)
        {
            query = query.Where(r => r.StoreId == sid);
        }

        var registers = await query.OrderBy(r => r.Code).ToListAsync(ct);
        var openShiftRegisterIds = await db.Shifts.AsNoTracking()
            .Where(s => s.Status == ShiftStatus.Open)
            .Select(s => s.RegisterId)
            .ToListAsync(ct);

        return registers.Select(r => new RegisterOptionDto
        {
            Id = r.Id,
            StoreId = r.StoreId,
            Display = $"{r.Store?.Code} · {r.Name} ({r.Code})",
            HasCashDrawer = r.HasCashDrawer,
            HasOpenShift = openShiftRegisterIds.Contains(r.Id)
        }).ToList();
    }

    private async Task<Result<Guid>> OpenShiftCoreAsync(OpenShiftDto dto, CancellationToken ct)
    {
        var register = await db.Registers.AsNoTracking()
            .Include(r => r.Store)
            .FirstOrDefaultAsync(r => r.Id == dto.RegisterId && r.IsActive, ct);

        if (register is null)
        {
            return Result<Guid>.Failure("Register not found.", "NOT_FOUND");
        }

        if (await db.Shifts.AnyAsync(s => s.RegisterId == dto.RegisterId && s.Status == ShiftStatus.Open, ct))
        {
            return Result<Guid>.Failure("This register already has an open shift.", "SHIFT_OPEN");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (await db.Shifts.AnyAsync(s => s.RegisterId == dto.RegisterId && s.Status == ShiftStatus.Open, ct))
            {
                await tx.RollbackAsync(ct);
                return Result<Guid>.Failure("This register already has an open shift.", "SHIFT_OPEN");
            }

            var now = clock.UtcNow;
            var shiftNumber = await GenerateShiftNumberAsync(
                register.Store?.Code ?? "STORE",
                register.Code,
                register.TenantId,
                register.StoreId,
                now,
                ct);

            var shift = new Shift
            {
                StoreId = register.StoreId,
                RegisterId = register.Id,
                ShiftNumber = shiftNumber,
                OpenedByUserId = currentUser.UserId ?? string.Empty,
                Status = ShiftStatus.Open,
                OpeningFloat = dto.OpeningFloat,
                OpenedOn = now,
                OpeningNotes = dto.Notes?.Trim()
            };

            db.Shifts.Add(shift);
            await db.SaveChangesAsync(ct);

            if (dto.OpeningFloat > 0)
            {
                await CashLedgerHelper.PostMovementAsync(
                    db, shift.Id, register.StoreId, CashMovementType.OpeningFloat,
                    dto.OpeningFloat, shiftNumber, "Opening float", null, null, now, ct);
                await db.SaveChangesAsync(ct);
            }

            await tx.CommitAsync(ct);
            return Result<Guid>.Success(shift.Id);
        });
    }

    private async Task<Result> RecordCashMovementCoreAsync(RecordCashMovementDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<CashMovementType>(dto.MovementType, true, out var movementType)
            || movementType is not (CashMovementType.CashIn or CashMovementType.CashOut))
        {
            return Result.Failure("Invalid movement type. Use CashIn or CashOut.", "VALIDATION");
        }

        if (dto.Amount <= 0)
        {
            return Result.Failure("Enter an amount greater than zero.", "VALIDATION");
        }

        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.ShiftId, ct);
        if (shift is null)
        {
            return Result.Failure("Shift not found.", "NOT_FOUND");
        }

        var amount = movementType == CashMovementType.CashOut ? -dto.Amount : dto.Amount;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var result = await CashLedgerHelper.PostMovementAsync(
                db, shift.Id, shift.StoreId, movementType, amount, null,
                dto.Notes?.Trim(), null, null, clock.UtcNow, ct);

            if (!result.Succeeded)
            {
                await tx.RollbackAsync(ct);
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }


    private async Task<Result> RecordNoSaleDrawerCoreAsync(NoSaleDrawerDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length < 3)
        {
            return Result.Failure("Enter a reason for opening the drawer without a sale.", "VALIDATION");
        }

        var shift = await db.Shifts.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.ShiftId, ct);
        if (shift is null)
        {
            return Result.Failure("Shift not found.", "NOT_FOUND");
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var result = await CashLedgerHelper.PostMovementAsync(
                db, shift.Id, shift.StoreId, CashMovementType.NoSaleDrawer, 0m, "NO-SALE",
                dto.Reason.Trim(), null, null, clock.UtcNow, ct);

            if (!result.Succeeded)
            {
                await tx.RollbackAsync(ct);
                return result;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> CloseShiftCoreAsync(CloseShiftDto dto, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var shift = await db.Shifts.FirstOrDefaultAsync(s => s.Id == dto.ShiftId, ct);
            if (shift is null)
            {
                return Result.Failure("Shift not found.", "NOT_FOUND");
            }

            if (shift.Status != ShiftStatus.Open)
            {
                return Result.Failure("Shift is already closed.", "ALREADY_CLOSED");
            }

            var expected = await CashLedgerHelper.GetBalanceAsync(db, shift.Id, ct);
            var denominationLines = (dto.Denominations ?? [])
                .Where(d => d.DenominationValue > 0 && d.Quantity > 0)
                .OrderByDescending(d => d.DenominationValue)
                .ToList();

            var denominationTotal = denominationLines.Sum(d => d.Amount);
            var countedCash = denominationLines.Count > 0 ? denominationTotal : dto.CountedCash;

            if (countedCash < 0)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure("Counted cash cannot be negative.", "VALIDATION");
            }

            shift.ExpectedCash = expected;
            shift.CountedCash = countedCash;
            shift.Variance = countedCash - expected;
            shift.ClosedByUserId = currentUser.UserId;
            shift.ClosedOn = clock.UtcNow;
            shift.ClosingNotes = dto.Notes?.Trim();
            shift.Status = ShiftStatus.Closed;

            var oldCounts = await db.CashDenominationCounts.Where(x => x.ShiftId == shift.Id).ToListAsync(ct);
            if (oldCounts.Count > 0)
            {
                db.CashDenominationCounts.RemoveRange(oldCounts);
            }

            var sort = 1;
            foreach (var line in denominationLines)
            {
                db.CashDenominationCounts.Add(new CashDenominationCount
                {
                    TenantId = shift.TenantId,
                    StoreId = shift.StoreId,
                    ShiftId = shift.Id,
                    DenominationValue = line.DenominationValue,
                    Quantity = line.Quantity,
                    Amount = line.Amount,
                    SortOrder = sort++
                });
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<ShiftReportDto?> GetReportCoreAsync(Guid shiftId, CancellationToken ct)
    {
        var shift = await db.Shifts.AsNoTracking()
            .Include(s => s.Register!).ThenInclude(r => r.Store)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct);

        if (shift is null)
        {
            return null;
        }

        var openedBy = await db.Users.AsNoTracking()
            .Where(u => u.Id == shift.OpenedByUserId)
            .Select(u => u.DisplayName ?? u.UserName ?? "User")
            .FirstOrDefaultAsync(ct) ?? "User";

        var movements = await db.CashMovements.AsNoTracking()
            .Where(m => m.ShiftId == shiftId)
            .ToListAsync(ct);

        var denominations = await db.CashDenominationCounts.AsNoTracking()
            .Where(d => d.ShiftId == shiftId && d.Quantity > 0)
            .OrderByDescending(d => d.DenominationValue)
            .Select(d => new CashDenominationLineDto
            {
                DenominationValue = d.DenominationValue,
                Quantity = d.Quantity,
                SortOrder = d.SortOrder
            })
            .ToListAsync(ct);

        var sales = await db.Sales.AsNoTracking()
            .Include(s => s.Payments)
            .Where(s => s.ShiftId == shiftId && s.Status != SaleStatus.Voided)
            .ToListAsync(ct);

        var cashSales = movements.Where(m => m.MovementType == CashMovementType.Sale).Sum(m => m.Amount);
        var cashRefunds = movements.Where(m => m.MovementType == CashMovementType.Refund).Sum(m => m.Amount);
        var cashIn = movements.Where(m => m.MovementType == CashMovementType.CashIn).Sum(m => m.Amount);
        var cashOut = movements.Where(m => m.MovementType == CashMovementType.CashOut).Sum(m => m.Amount);
        var noSaleCount = movements.Count(m => m.MovementType == CashMovementType.NoSaleDrawer);
        var expected = shift.Status == ShiftStatus.Open
            ? await CashLedgerHelper.GetBalanceAsync(db, shiftId, ct)
            : shift.ExpectedCash;

        return new ShiftReportDto
        {
            ShiftId = shift.Id,
            ShiftNumber = shift.ShiftNumber,
            RegisterName = shift.Register?.Name ?? "",
            StoreName = shift.Register?.Store is { } store ? $"{store.Code} · {store.Name}" : "",
            OpenedByName = openedBy,
            Status = shift.Status.ToString(),
            OpenedOn = shift.OpenedOn,
            ClosedOn = shift.ClosedOn,
            OpeningFloat = shift.OpeningFloat,
            CashSales = cashSales,
            CashRefunds = cashRefunds,
            CashIn = cashIn,
            CashOut = Math.Abs(cashOut),
            NoSaleDrawerOpenCount = noSaleCount,
            ExpectedCash = expected,
            CountedCash = shift.CountedCash,
            Variance = shift.Variance,
            SaleCount = sales.Count,
            CardSales = sales.SelectMany(s => s.Payments).Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount),
            CreditSales = sales.SelectMany(s => s.Payments).Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount),
            GrossSales = sales.Sum(s => s.TotalAmount),
            Denominations = denominations
        };
    }

    private Task<string> GenerateShiftNumberAsync(
        string storeCode,
        string registerCode,
        Guid tenantId,
        Guid storeId,
        DateTime now,
        CancellationToken ct)
    {
        var prefix = $"{NormalizeCode(storeCode)}-{NormalizeCode(registerCode)}-SHIFT-{now:yyyyMMdd}-";
        return numberSequence.GetNextAsync(tenantId, storeId, "SHIFT", prefix, now, 3, ct);
    }

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "NA" : code.Trim().ToUpperInvariant().Replace(" ", "");
}
