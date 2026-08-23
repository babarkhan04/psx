using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class RegisterService(AppDbContext db) : IRegisterService
{
    public async Task<IReadOnlyList<RegisterDto>> GetAllAsync(Guid? storeId = null, CancellationToken ct = default)
    {
        var query = db.Registers.AsNoTracking().Include(r => r.Store).AsQueryable();
        if (storeId is not null)
        {
            query = query.Where(r => r.StoreId == storeId);
        }

        return await query
            .OrderBy(r => r.Store!.Code).ThenBy(r => r.Code)
            .Select(r => Map(r))
            .ToListAsync(ct);
    }

    public async Task<RegisterDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var register = await db.Registers.AsNoTracking().Include(r => r.Store)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        return register is null ? null : Map(register);
    }

    public async Task<Result<Guid>> CreateAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (!await db.Stores.AnyAsync(s => s.Id == dto.StoreId, ct))
        {
            return Result<Guid>.Failure("Selected store does not exist.", "INVALID_STORE");
        }

        var code = dto.Code.Trim();
        if (await db.Registers.AnyAsync(r => r.StoreId == dto.StoreId && r.Code == code, ct))
        {
            return Result<Guid>.Failure($"A register with code '{code}' already exists in this store.", "DUPLICATE_CODE");
        }

        var register = new Register { StoreId = dto.StoreId };
        Apply(register, dto);
        db.Registers.Add(register);
        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(register.Id);
    }

    public async Task<Result> UpdateAsync(RegisterDto dto, CancellationToken ct = default)
    {
        var register = await db.Registers.FirstOrDefaultAsync(r => r.Id == dto.Id, ct);
        if (register is null)
        {
            return Result.Failure("Register not found.", "NOT_FOUND");
        }

        var code = dto.Code.Trim();
        if (await db.Registers.AnyAsync(r => r.StoreId == dto.StoreId && r.Code == code && r.Id != dto.Id, ct))
        {
            return Result.Failure($"A register with code '{code}' already exists in this store.", "DUPLICATE_CODE");
        }

        register.StoreId = dto.StoreId;
        Apply(register, dto);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SetActiveAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var register = await db.Registers.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (register is null)
        {
            return Result.Failure("Register not found.", "NOT_FOUND");
        }

        register.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var register = await db.Registers.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (register is null)
        {
            return Result.Failure("Register not found.", "NOT_FOUND");
        }

        db.Registers.Remove(register);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private static void Apply(Register register, RegisterDto dto)
    {
        register.Code = dto.Code.Trim();
        register.Name = dto.Name.Trim();
        register.Location = dto.Location?.Trim();
        register.PrinterName = dto.PrinterName?.Trim();
        register.HasCashDrawer = dto.HasCashDrawer;
        register.IsActive = dto.IsActive;
    }

    private static RegisterDto Map(Register r) => new()
    {
        Id = r.Id,
        StoreId = r.StoreId,
        StoreName = r.Store is null ? null : $"{r.Store.Code} · {r.Store.Name}",
        Code = r.Code,
        Name = r.Name,
        Location = r.Location,
        PrinterName = r.PrinterName,
        HasCashDrawer = r.HasCashDrawer,
        IsActive = r.IsActive
    };
}
