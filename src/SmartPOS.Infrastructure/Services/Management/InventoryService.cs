using Microsoft.EntityFrameworkCore;
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
    IUserContextAccessor userContext) : IInventoryService
{
    public Task<IReadOnlyList<StockListItemDto>> GetStockAsync(
        Guid? storeId = null,
        bool lowStockOnly = false,
        CancellationToken ct = default) =>
        WithReadDbAsync(readDb => GetStockCoreAsync(readDb, storeId, lowStockOnly, ct), ct);

    public Task<IReadOnlyList<StockLedgerDto>> GetLedgerAsync(
        Guid? storeId = null,
        Guid? productId = null,
        int take = 100,
        CancellationToken ct = default) =>
        WithReadDbAsync(readDb => GetLedgerCoreAsync(readDb, storeId, productId, take, ct), ct);

    private async Task<T> WithReadDbAsync<T>(Func<AppDbContext, Task<T>> operation, CancellationToken ct)
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
        return await operation(readDb);
    }

    private static async Task<IReadOnlyList<StockListItemDto>> GetStockCoreAsync(
        AppDbContext readDb,
        Guid? storeId,
        bool lowStockOnly,
        CancellationToken ct)
    {
        var query = readDb.Stocks.AsNoTracking().AsQueryable();

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
        Guid? storeId,
        Guid? productId,
        int take,
        CancellationToken ct)
    {
        var query = readDb.StockLedgers.AsNoTracking().AsQueryable();

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

    public async Task<Result> PostAdjustmentAsync(StockAdjustmentDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
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

        // SqlServerRetryingExecutionStrategy forbids user-initiated transactions unless
        // the entire unit (including the transaction) is wrapped inside ExecuteAsync.
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            foreach (var line in dto.Lines.Where(l => l.QuantityChange != 0))
            {
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

    public async Task<Result> PostOpeningStockAsync(
        Guid storeId,
        Guid productId,
        decimal quantity,
        Guid? variantId = null,
        CancellationToken ct = default)
    {
        if (quantity < 0)
        {
            return Result.Failure("Opening stock cannot be negative.", "INVALID_QTY");
        }

        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == productId && s.ProductVariantId == variantId, ct);

        if (stock is not null && stock.QuantityOnHand > 0)
        {
            return Result.Failure("Opening stock already posted for this item. Use adjustment instead.", "ALREADY_SET");
        }

        var change = quantity - (stock?.QuantityOnHand ?? 0);
        if (change == 0)
        {
            return Result.Success();
        }

        var result = await ApplyMovementAsync(
            storeId, productId, variantId, StockMovementType.Opening, change, "OPENING", null, ct);
        if (!result.Succeeded)
        {
            return result;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
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
        if (!await db.Products.AnyAsync(p => p.Id == productId, ct))
        {
            return Result.Failure("Product not found.", "NOT_FOUND");
        }

        if (!await db.Stores.AnyAsync(s => s.Id == storeId, ct))
        {
            return Result.Failure("Store not found.", "NOT_FOUND");
        }

        var stock = await db.Stocks
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == productId && s.ProductVariantId == variantId, ct);

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
