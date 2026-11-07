using System.Data;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class PurchaseService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    INumberSequenceService numberSequence,
    DbContextGate gate) : IPurchaseService
{
    public Task<IReadOnlyList<PurchaseListItemDto>> GetAllAsync(PurchaseQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetAllCoreAsync(query, ct), ct);

    public Task<PurchaseDetailDto?> GetAsync(Guid id, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetCoreAsync(id, ct), ct);

    public Task<Result<Guid>> CreateDraftAsync(PurchaseEditDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SavePurchaseCoreAsync(dto, receive: false, ct), ct);

    public Task<Result<Guid>> CreateAndReceiveAsync(PurchaseEditDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => SavePurchaseCoreAsync(dto, receive: true, ct), ct);

    public Task<Result> ReceiveAsync(Guid purchaseId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ReceiveCoreAsync(purchaseId, ct), ct);

    public Task<Result> CancelAsync(Guid purchaseId, CancellationToken ct = default) =>
        gate.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            var purchase = await db.Purchases.FirstOrDefaultAsync(p => p.Id == purchaseId, ct);
            if (purchase is null)
            {
                return Result.Failure("Purchase not found.", "NOT_FOUND");
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                return Result.Failure("Only draft purchases can be cancelled.", "INVALID_STATUS");
            }

            purchase.Status = PurchaseStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            return Result.Success();
        }, ct);

    private async Task<IReadOnlyList<PurchaseListItemDto>> GetAllCoreAsync(PurchaseQueryDto query, CancellationToken ct)
    {
        var q = db.Purchases.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
            .AsQueryable();

        if (query.StoreId is { } storeId)
        {
            q = q.Where(p => p.StoreId == storeId);
        }

        if (query.SupplierId is { } supplierId)
        {
            q = q.Where(p => p.SupplierId == supplierId);
        }

        if (!string.IsNullOrWhiteSpace(query.Status)
            && Enum.TryParse<PurchaseStatus>(query.Status, true, out var status))
        {
            q = q.Where(p => p.Status == status);
        }

        if (query.FromUtc is { } from)
        {
            q = q.Where(p => (p.ReceivedOn ?? p.OrderedOn ?? p.CreatedOn) >= from);
        }

        if (query.ToUtc is { } to)
        {
            q = q.Where(p => (p.ReceivedOn ?? p.OrderedOn ?? p.CreatedOn) < to);
        }

        var storeNames = await db.Stores.AsNoTracking()
            .ToDictionaryAsync(s => s.Id, s => $"{s.Code} · {s.Name}", ct);

        var purchases = await q.OrderByDescending(p => p.ReceivedOn ?? p.OrderedOn ?? p.CreatedOn)
            .Take(query.Take <= 0 ? 100 : query.Take)
            .ToListAsync(ct);

        return purchases.Select(p => new PurchaseListItemDto
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            SupplierName = p.Supplier?.Name ?? "",
            StoreName = storeNames.GetValueOrDefault(p.StoreId, ""),
            Status = p.Status.ToString(),
            LineCount = p.Lines.Count,
            TotalAmount = p.TotalAmount,
            ReceivedOn = p.ReceivedOn,
            OrderedOn = p.OrderedOn
        }).ToList();
    }

    private async Task<PurchaseDetailDto?> GetCoreAsync(Guid id, CancellationToken ct)
    {
        var p = await db.Purchases.AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, ct);

        if (p is null)
        {
            return null;
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == p.StoreId, ct);

        return new PurchaseDetailDto
        {
            Id = p.Id,
            PurchaseNumber = p.PurchaseNumber,
            SupplierName = p.Supplier?.Name ?? "",
            StoreName = store is null ? "" : $"{store.Code} · {store.Name}",
            Status = p.Status.ToString(),
            Subtotal = p.Subtotal,
            TaxAmount = p.TaxAmount,
            TotalAmount = p.TotalAmount,
            Notes = p.Notes,
            ReceivedOn = p.ReceivedOn,
            Lines = p.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseLineDetailDto
            {
                Sku = l.Sku,
                ProductName = l.ProductName,
                Quantity = l.Quantity,
                UnitCost = l.UnitCost,
                LineTotal = l.LineTotal
            }).ToList()
        };
    }

    private async Task<Result<Guid>> SavePurchaseCoreAsync(PurchaseEditDto dto, bool receive, CancellationToken ct)
    {
        if (dto.Lines.Count == 0)
        {
            return Result<Guid>.Failure("Add at least one product line.", "NO_LINES");
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.StoreId, ct);
        if (store is null)
        {
            return Result<Guid>.Failure("Store not found.", "NOT_FOUND");
        }

        var supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.SupplierId && s.IsActive, ct);
        if (supplier is null)
        {
            return Result<Guid>.Failure("Supplier not found.", "NOT_FOUND");
        }

        var lineData = await BuildLinesAsync(dto.Lines, ct);
        if (!lineData.Succeeded)
        {
            return Result<Guid>.Failure(lineData.Error!, lineData.ErrorCode!);
        }

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var now = clock.UtcNow;
            var purchaseNumber = await GeneratePurchaseNumberAsync(store.Code, store.TenantId, dto.StoreId, now, ct);

            var purchase = new Purchase
            {
                StoreId = dto.StoreId,
                SupplierId = dto.SupplierId,
                PurchaseNumber = purchaseNumber,
                Status = PurchaseStatus.Draft,
                ReceivedByUserId = currentUser.UserId ?? string.Empty,
                Subtotal = lineData.Data!.Subtotal,
                TaxAmount = lineData.Data.TaxAmount,
                TotalAmount = lineData.Data.TotalAmount,
                Notes = dto.Notes?.Trim(),
                OrderedOn = now
            };

            db.Purchases.Add(purchase);

            var lineNumber = 1;
            foreach (var line in lineData.Data.Lines)
            {
                db.PurchaseLines.Add(new PurchaseLine
                {
                    PurchaseId = purchase.Id,
                    LineNumber = lineNumber++,
                    ProductId = line.ProductId,
                    ProductVariantId = line.VariantId,
                    Sku = line.Sku,
                    ProductName = line.Name,
                    Quantity = line.Quantity,
                    UnitCost = line.UnitCost,
                    TaxRate = line.TaxRate,
                    TaxAmount = line.TaxAmount,
                    LineTotal = line.LineTotal
                });
            }

            if (receive)
            {
                var receiveResult = await PostReceiveStockAsync(purchase, lineData.Data.Lines, dto.StoreId, purchaseNumber, now, ct);
                if (!receiveResult.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return Result<Guid>.Failure(receiveResult.Error!, receiveResult.ErrorCode!);
                }

                purchase.Status = PurchaseStatus.Received;
                purchase.ReceivedOn = now;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<Guid>.Success(purchase.Id);
        });
    }

    private async Task<Result> ReceiveCoreAsync(Guid purchaseId, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var purchase = await db.Purchases
                .Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == purchaseId, ct);

            if (purchase is null)
            {
                return Result.Failure("Purchase not found.", "NOT_FOUND");
            }

            if (purchase.Status != PurchaseStatus.Draft)
            {
                return Result.Failure("Only draft purchases can be received.", "INVALID_STATUS");
            }

            var now = clock.UtcNow;
            var lines = purchase.Lines.Select(l => new BuiltLine(
                l.ProductId, l.ProductVariantId, l.Sku, l.ProductName,
                l.Quantity, l.UnitCost, l.TaxRate, l.TaxAmount, l.LineTotal)).ToList();

            var receiveResult = await PostReceiveStockAsync(purchase, lines, purchase.StoreId, purchase.PurchaseNumber, now, ct);
            if (!receiveResult.Succeeded)
            {
                await tx.RollbackAsync(ct);
                return receiveResult;
            }

            purchase.Status = PurchaseStatus.Received;
            purchase.ReceivedOn = now;
            purchase.ReceivedByUserId = currentUser.UserId ?? purchase.ReceivedByUserId;

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> PostReceiveStockAsync(
        Purchase purchase,
        IReadOnlyList<BuiltLine> lines,
        Guid storeId,
        string reference,
        DateTime now,
        CancellationToken ct)
    {
        foreach (var line in lines)
        {
            var stock = await db.Stocks.FirstOrDefaultAsync(
                s => s.StoreId == storeId && s.ProductId == line.ProductId && s.ProductVariantId == line.VariantId, ct);

            if (stock is null)
            {
                stock = new Stock
                {
                    StoreId = storeId,
                    ProductId = line.ProductId,
                    ProductVariantId = line.VariantId,
                    QuantityOnHand = 0
                };
                db.Stocks.Add(stock);
            }

            stock.QuantityOnHand += line.Quantity;
            db.StockLedgers.Add(new StockLedger
            {
                StoreId = storeId,
                ProductId = line.ProductId,
                ProductVariantId = line.VariantId,
                MovementType = StockMovementType.Purchase,
                QuantityChange = line.Quantity,
                QuantityAfter = stock.QuantityOnHand,
                Reference = reference,
                Notes = purchase.Notes
            });

            var product = await db.Products.FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
            if (product is not null)
            {
                product.CostPrice = line.UnitCost;
            }
        }

        return Result.Success();
    }

    private async Task<Result<BuiltPurchase>> BuildLinesAsync(IList<PurchaseLineInputDto> inputs, CancellationToken ct)
    {
        var productIds = inputs.Select(l => l.ProductId).Distinct().ToList();
        var variantIds = inputs.Where(l => l.ProductVariantId is not null).Select(l => l.ProductVariantId!.Value).Distinct().ToList();

        var products = await db.Products.AsNoTracking()
            .Include(p => p.TaxCategory)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, ct);

        var variants = await db.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .ToDictionaryAsync(v => v.Id, ct);

        var lines = new List<BuiltLine>();
        foreach (var input in inputs)
        {
            if (!products.TryGetValue(input.ProductId, out var product))
            {
                return Result<BuiltPurchase>.Failure("Product not found.", "NOT_FOUND");
            }

            if (input.Quantity <= 0)
            {
                return Result<BuiltPurchase>.Failure("Quantity must be greater than zero.", "VALIDATION");
            }

            ProductVariant? variant = null;
            if (input.ProductVariantId is { } variantId)
            {
                if (!variants.TryGetValue(variantId, out variant) || variant.ProductId != product.Id || !variant.IsActive)
                {
                    return Result<BuiltPurchase>.Failure($"Variant not found for {product.Name}.", "VARIANT_NOT_FOUND");
                }
            }

            var subtotal = Math.Round(input.Quantity * input.UnitCost, 2);
            var taxRate = product.TaxCategory?.Rate ?? 0m;
            var tax = Math.Round(subtotal * taxRate / 100m, 2);
            var sku = variant?.Sku ?? product.Sku;
            var name = variant is null ? product.Name : $"{product.Name} - {variant.Name}";

            lines.Add(new BuiltLine(
                product.Id, variant?.Id, sku, name,
                input.Quantity, input.UnitCost, taxRate, tax, subtotal + tax));
        }

        return Result<BuiltPurchase>.Success(new BuiltPurchase
        {
            Lines = lines,
            Subtotal = lines.Sum(l => l.Quantity * l.UnitCost),
            TaxAmount = lines.Sum(l => l.TaxAmount),
            TotalAmount = lines.Sum(l => l.LineTotal)
        });
    }

    private Task<string> GeneratePurchaseNumberAsync(string storeCode, Guid tenantId, Guid storeId, DateTime now, CancellationToken ct)
    {
        var prefix = $"{NormalizeCode(storeCode)}-PO-{now:yyyyMMdd}-";
        return numberSequence.GetNextAsync(tenantId, storeId, "PURCHASE", prefix, now, 4, ct);
    }

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "NA" : code.Trim().ToUpperInvariant().Replace(" ", "");

    private sealed record BuiltLine(
        Guid ProductId, Guid? VariantId, string Sku, string Name,
        decimal Quantity, decimal UnitCost, decimal TaxRate, decimal TaxAmount, decimal LineTotal);

    private sealed class BuiltPurchase
    {
        public IReadOnlyList<BuiltLine> Lines { get; init; } = [];
        public decimal Subtotal { get; init; }
        public decimal TaxAmount { get; init; }
        public decimal TotalAmount { get; init; }
    }
}
