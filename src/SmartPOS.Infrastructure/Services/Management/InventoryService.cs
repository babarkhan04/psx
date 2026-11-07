using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Domain.Authorization;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class InventoryService(
    AppDbContext db,
    IServiceScopeFactory scopeFactory,
    ITenantContext tenantContext,
    IUserContextAccessor userContext,
    IStoreAccessService storeAccess,
    DbContextGate gate) : IInventoryService
{
    public Task<IReadOnlyList<StockListItemDto>> GetStockAsync(
        Guid? storeId = null,
        bool lowStockOnly = false,
        CancellationToken ct = default) =>
        WithReadDbAsync(async (readDb, readAccess) =>
        {
            var allowed = await readAccess.GetAllowedStoreIdsAsync(ct);
            if (storeId is { } requestedStore)
                await readAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
            return await GetStockCoreAsync(readDb, allowed, storeId, lowStockOnly, ct);
        }, ct);

    public Task<IReadOnlyList<StockLedgerDto>> GetLedgerAsync(
        Guid? storeId = null,
        Guid? productId = null,
        int take = 100,
        CancellationToken ct = default) =>
        WithReadDbAsync(async (readDb, readAccess) =>
        {
            var allowed = await readAccess.GetAllowedStoreIdsAsync(ct);
            if (storeId is { } requestedStore)
                await readAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
            return await GetLedgerCoreAsync(readDb, allowed, storeId, productId, take, ct);
        }, ct);

    private async Task<T> WithReadDbAsync<T>(Func<AppDbContext, IStoreAccessService, Task<T>> operation, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var tenantId = tenantContext.TenantId;
        var principal = userContext.Principal;

        await using var scope = scopeFactory.CreateAsyncScope();

        if (principal is not null)
        {
            scope.ServiceProvider.GetRequiredService<IUserContextAccessor>().SetPrincipal(principal);
        }

        if (tenantId is { } id)
        {
            scope.ServiceProvider.GetRequiredService<ITenantContext>().SetTenant(id);
        }

        var readDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await operation(readDb, scope.ServiceProvider.GetRequiredService<IStoreAccessService>());
    }

    private static async Task<IReadOnlyList<StockListItemDto>> GetStockCoreAsync(
        AppDbContext readDb,
        IReadOnlyList<Guid> allowed,
        Guid? storeId,
        bool lowStockOnly,
        CancellationToken ct)
    {
        var query = readDb.Stocks.AsNoTracking().Where(stock => allowed.Contains(stock.StoreId));

        if (storeId is not null)
        {
            query = query.Where(s => s.StoreId == storeId);
        }

        if (lowStockOnly)
        {
            query = query.Where(s => s.Product != null
                && s.Product.ReorderLevel > 0
                && s.QuantityOnHand <= s.Product.ReorderLevel);
        }

        return await query
            .OrderBy(s => s.Product!.Name)
            .ThenBy(s => s.Store!.Name)
            .Select(s => new StockListItemDto
            {
                Id = s.Id,
                ProductId = s.ProductId,
                ProductSku = s.Product != null ? s.Product.Sku : string.Empty,
                ProductName = s.Product != null ? s.Product.Name : string.Empty,
                VariantName = s.ProductVariant != null ? s.ProductVariant.Name : null,
                StoreId = s.StoreId,
                StoreName = s.Store != null ? s.Store.Code + " · " + s.Store.Name : string.Empty,
                QuantityOnHand = s.QuantityOnHand,
                ReorderLevel = s.Product != null ? s.Product.ReorderLevel : 0,
                IsLowStock = s.Product != null
                    && s.Product.ReorderLevel > 0
                    && s.QuantityOnHand <= s.Product.ReorderLevel
            })
            .ToListAsync(ct);
    }

    private static async Task<IReadOnlyList<StockLedgerDto>> GetLedgerCoreAsync(
        AppDbContext readDb,
        IReadOnlyList<Guid> allowed,
        Guid? storeId,
        Guid? productId,
        int take,
        CancellationToken ct)
    {
        var query = readDb.StockLedgers.AsNoTracking().Where(ledger => allowed.Contains(ledger.StoreId));

        if (storeId is not null)
        {
            query = query.Where(l => l.StoreId == storeId);
        }

        if (productId is not null)
        {
            query = query.Where(l => l.ProductId == productId);
        }

        var rowLimit = Math.Clamp(take, 1, 500);

        return await query
            .OrderByDescending(l => l.CreatedOn)
            .Take(rowLimit)
            .Select(l => new StockLedgerDto
            {
                Id = l.Id,
                CreatedOn = l.CreatedOn,
                CreatedBy = l.CreatedBy,
                MovementType = l.MovementType.ToString(),
                ProductName = l.Product != null ? l.Product.Name : string.Empty,
                VariantName = l.ProductVariant != null ? l.ProductVariant.Name : null,
                StoreName = l.Store != null ? l.Store.Name : string.Empty,
                QuantityChange = l.QuantityChange,
                QuantityAfter = l.QuantityAfter,
                Reference = l.Reference,
                Notes = l.Notes
            })
            .ToListAsync(ct);
    }

    public Task<Result> PostAdjustmentAsync(StockAdjustmentDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => PostAdjustmentCoreAsync(dto, ct), ct);

    public Task<Result> PostOpeningStockAsync(Guid storeId, Guid productId, decimal quantity, Guid? variantId = null, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => PostOpeningStockCoreAsync(storeId, productId, quantity, variantId, ct), ct);

    private async Task<Result> PostAdjustmentCoreAsync(StockAdjustmentDto dto, CancellationToken ct)
    {
        EnsureManagePermission();
        await storeAccess.EnsureCanAccessStoreAsync(dto.StoreId, ct);
        if (dto.Lines is null || dto.Lines.Count == 0 || dto.Lines.Count > 500 || dto.Lines.Any(line => line is null))
        {
            return Result.Failure("Add at least one product line.", "NO_LINES");
        }

        if (!Enum.TryParse<StockMovementType>(dto.MovementType, out var movementType))
        {
            return Result.Failure("Invalid movement type.", "INVALID_TYPE");
        }

        if (movementType is not (StockMovementType.Adjustment or StockMovementType.Damage or StockMovementType.Loss or StockMovementType.Opening))
        {
            return Result.Failure("Invalid movement type for adjustment.", "INVALID_TYPE");
        }

        if (movementType == StockMovementType.Opening && (dto.Lines.Any(line => line.QuantityChange < 0) ||
            dto.Lines.GroupBy(line => new { line.ProductId, line.ProductVariantId }).Any(group => group.Count() > 1)))
            return Result.Failure("Use one non-negative opening line per item.", "INVALID_OPENING");

        // SqlServerRetryingExecutionStrategy forbids user-initiated transactions unless
        // the entire unit (including the transaction) is wrapped inside ExecuteAsync.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            foreach (var line in dto.Lines.Where(l => l.QuantityChange != 0))
            {
                if (movementType == StockMovementType.Opening &&
                    (await db.StockLedgers.AnyAsync(ledger => ledger.StoreId == dto.StoreId && ledger.ProductId == line.ProductId &&
                        ledger.ProductVariantId == line.ProductVariantId, ct) ||
                     await db.Stocks.AnyAsync(stock => stock.StoreId == dto.StoreId && stock.ProductId == line.ProductId &&
                        stock.ProductVariantId == line.ProductVariantId && stock.QuantityOnHand != 0, ct)))
                    return Result.Failure("This item already has stock history. Use an adjustment instead.", "ALREADY_SET");
                var result = await ApplyMovementAsync(
                    dto.StoreId,
                    line.ProductId,
                    line.ProductVariantId,
                    movementType,
                    line.QuantityChange,
                    dto.Reference,
                    dto.Notes,
                    ct);

                if (!result.Succeeded)
                {
                    return result;
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> PostOpeningStockCoreAsync(Guid storeId, Guid productId, decimal quantity,
        Guid? variantId, CancellationToken ct)
    {
        EnsureManagePermission();
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        if (quantity < 0 || quantity > 999999999m || Math.Round(quantity, 3) != quantity)
            return Result.Failure("Enter a non-negative opening quantity with at most three decimal places.", "INVALID_QTY");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if (await db.StockLedgers.AnyAsync(ledger => ledger.StoreId == storeId && ledger.ProductId == productId &&
                    ledger.ProductVariantId == variantId, ct))
                return Result.Failure("This item already has stock history. Use an adjustment instead.", "ALREADY_SET");
            var stock = await db.Stocks.FirstOrDefaultAsync(row => row.StoreId == storeId && row.ProductId == productId &&
                row.ProductVariantId == variantId, ct);
            if (stock is not null && stock.QuantityOnHand != 0)
                return Result.Failure("Opening stock already exists. Use an adjustment instead.", "ALREADY_SET");
            var result = await ApplyMovementAsync(storeId, productId, variantId, StockMovementType.Opening,
                quantity, "OPENING", null, ct);
            if (!result.Succeeded)
                return result;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private void EnsureManagePermission()
    {
        if (userContext.Principal?.Identity?.IsAuthenticated != true ||
            !userContext.Principal.HasClaim(Permissions.ClaimType, Permissions.Catalog.InventoryManage))
            throw new UnauthorizedAccessException("You do not have permission to change inventory.");
    }

    private async Task<Result> ApplyMovementAsync(
        Guid storeId,
        Guid productId,
        Guid? variantId,
        StockMovementType movementType,
        decimal quantityChange,
        string? reference,
        string? notes,
        CancellationToken ct)
    {
        if (quantityChange > 999999999m || quantityChange < -999999999m || Math.Round(quantityChange, 3) != quantityChange)
            return Result.Failure("Stock quantities must use at most three decimal places.", "INVALID_QTY");
        if (variantId is { } id && !await db.ProductVariants.AnyAsync(v => v.Id == id && v.ProductId == productId, ct))
            return Result.Failure("Variant does not belong to this product.", "INVALID_VARIANT");
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            return Result.Failure("Product not found.", "NOT_FOUND");
        }

        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct))
        {
            return Result.Failure("Store not found.", "NOT_FOUND");
        }

        var stock = db.Stocks.Local.FirstOrDefault(s => s.StoreId == storeId && s.ProductId == productId && s.ProductVariantId == variantId)
            ?? await db.Stocks.FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == productId && s.ProductVariantId == variantId, ct);

        if (stock is null)
        {
            stock = new Stock
            {
                StoreId = storeId,
                ProductId = productId,
                ProductVariantId = variantId,
                QuantityOnHand = 0
            };
            db.Stocks.Add(stock);
        }

        var newQty = stock.QuantityOnHand + quantityChange;
        if (newQty < 0)
        {
            return Result.Failure($"Insufficient stock for {movementType}. Resulting quantity would be {newQty}.", "INSUFFICIENT");
        }

        stock.QuantityOnHand = newQty;

        db.StockLedgers.Add(new StockLedger
        {
            StoreId = storeId,
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = movementType,
            QuantityChange = quantityChange,
            QuantityAfter = newQty,
            Reference = reference,
            Notes = notes
        });

        return Result.Success();
    }
}
