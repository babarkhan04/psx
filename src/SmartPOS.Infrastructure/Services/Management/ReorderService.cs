using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class ReorderService(
    AppDbContext db,
    DbContextGate gate,
    IPurchaseService purchaseService,
    IStockTransferService transferService) : IReorderService
{
    public Task<ReorderDashboardDto> GetReorderDashboardAsync(ReorderQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetDashboardCoreAsync(Normalize(query), ct), ct);

    public Task<Result<Guid>> CreatePurchaseDraftAsync(CreateReorderPurchaseDraftDto dto, CancellationToken ct = default)
    {
        var lines = dto.Lines
            .Where(l => l.ProductId != Guid.Empty && l.Quantity > 0)
            .GroupBy(l => new { l.ProductId, l.ProductVariantId })
            .Select(g => new PurchaseLineInputDto
            {
                ProductId = g.Key.ProductId,
                ProductVariantId = g.Key.ProductVariantId,
                Quantity = RoundQty(g.Sum(x => x.Quantity)),
                UnitCost = Math.Max(0, g.Last().UnitCost)
            })
            .ToList();

        if (dto.StoreId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure("Store is required.", "STORE_REQUIRED"));
        }

        if (dto.SupplierId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure("Supplier is required before creating a purchase draft.", "SUPPLIER_REQUIRED"));
        }

        if (lines.Count == 0)
        {
            return Task.FromResult(Result<Guid>.Failure("Add at least one reorder line with quantity greater than zero.", "NO_LINES"));
        }

        return purchaseService.CreateDraftAsync(new PurchaseEditDto
        {
            StoreId = dto.StoreId,
            SupplierId = dto.SupplierId,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? "Draft purchase created from Smart Reorder."
                : dto.Notes.Trim(),
            Lines = lines
        }, ct);
    }

    public Task<Result<Guid>> CreateTransferRequestAsync(CreateReorderTransferRequestDto dto, CancellationToken ct = default)
    {
        var lines = dto.Lines
            .Where(l => l.ProductId != Guid.Empty && l.Quantity > 0)
            .GroupBy(l => new { l.ProductId, l.ProductVariantId })
            .Select(g => new CreateStockTransferLineDto
            {
                ProductId = g.Key.ProductId,
                ProductVariantId = g.Key.ProductVariantId,
                Quantity = RoundQty(g.Sum(x => x.Quantity))
            })
            .ToList();

        if (dto.SourceStoreId == Guid.Empty || dto.DestinationStoreId == Guid.Empty)
        {
            return Task.FromResult(Result<Guid>.Failure("Source and destination stores are required.", "STORE_REQUIRED"));
        }

        if (dto.SourceStoreId == dto.DestinationStoreId)
        {
            return Task.FromResult(Result<Guid>.Failure("Source and destination stores cannot be the same.", "SAME_STORE"));
        }

        if (lines.Count == 0)
        {
            return Task.FromResult(Result<Guid>.Failure("Add at least one transfer line with quantity greater than zero.", "NO_LINES"));
        }

        return transferService.CreateRequestedAsync(new CreateStockTransferDto
        {
            SourceStoreId = dto.SourceStoreId,
            DestinationStoreId = dto.DestinationStoreId,
            Notes = string.IsNullOrWhiteSpace(dto.Notes)
                ? "Transfer request created from Smart Reorder."
                : dto.Notes.Trim(),
            Lines = lines
        }, ct);
    }

    private async Task<ReorderDashboardDto> GetDashboardCoreAsync(ReorderQueryDto query, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var fromUtc = now.Date.AddDays(-query.LookbackDays);
        var deadStockFromUtc = now.Date.AddDays(-query.DeadStockDays);

        var stores = await db.Stores.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .Select(s => new StoreRow(s.Id, s.Code + " · " + s.Name))
            .ToListAsync(ct);

        if (query.StoreId is { } selectedStoreId && selectedStoreId != Guid.Empty)
        {
            stores = stores.Where(s => s.Id == selectedStoreId).ToList();
        }

        var storeIds = stores.Select(s => s.Id).ToHashSet();
        if (storeIds.Count == 0)
        {
            return new ReorderDashboardDto();
        }

        var storeAssignments = await db.ProductStoreAssignments.AsNoTracking()
            .Where(a => storeIds.Contains(a.StoreId) && a.IsVisibleOnPos)
            .Select(a => new StoreAssignmentRow(a.StoreId, a.ProductId))
            .ToListAsync(ct);

        if (storeAssignments.Count == 0)
        {
            return new ReorderDashboardDto();
        }

        var productQuery = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Where(p => p.IsActive && p.StoreAssignments.Any(a => storeIds.Contains(a.StoreId) && a.IsVisibleOnPos));

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            productQuery = productQuery.Where(p =>
                EF.Functions.Like(p.Sku, $"%{term}%") ||
                EF.Functions.Like(p.Name, $"%{term}%") ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{term}%")));
        }

        var products = await productQuery
            .OrderBy(p => p.Sku)
            .Select(p => new ProductRow(
                p.Id,
                p.Sku,
                p.Name,
                p.Category == null ? null : p.Category.Name,
                p.Brand == null ? null : p.Brand.Name,
                p.CostPrice,
                p.SellPrice,
                p.ReorderLevel))
            .ToListAsync(ct);

        if (products.Count == 0)
        {
            return new ReorderDashboardDto();
        }

        var productIds = products.Select(p => p.Id).ToHashSet();
        var productMap = products.ToDictionary(p => p.Id);
        var storeNameMap = stores.ToDictionary(s => s.Id, s => s.Name);

        var stockRows = await db.Stocks.AsNoTracking()
            .Include(s => s.ProductVariant)
            .Where(s => storeIds.Contains(s.StoreId) && productIds.Contains(s.ProductId))
            .Select(s => new StockRow(
                s.StoreId,
                s.ProductId,
                s.ProductVariantId,
                s.ProductVariant == null ? null : s.ProductVariant.Name,
                s.ProductVariant == null ? null : s.ProductVariant.Sku,
                s.ProductVariant == null ? null : s.ProductVariant.CostPrice,
                s.QuantityOnHand))
            .ToListAsync(ct);

        var candidateKeys = BuildCandidateKeys(storeAssignments, products, stockRows);

        var saleQty = await db.SaleLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId)
                && l.Sale != null
                && storeIds.Contains(l.Sale.StoreId)
                && l.Sale.CompletedOn >= fromUtc
                && l.Sale.CompletedOn <= now
                && l.Sale.Status != SaleStatus.Voided)
            .GroupBy(l => new { l.Sale!.StoreId, l.ProductId, l.ProductVariantId })
            .Select(g => new MovementQtyRow(g.Key.StoreId, g.Key.ProductId, g.Key.ProductVariantId, g.Sum(x => x.Quantity)))
            .ToListAsync(ct);

        var returnedQty = await db.SaleReturnLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId)
                && l.SaleReturn != null
                && storeIds.Contains(l.SaleReturn.StoreId)
                && l.SaleReturn.ProcessedOn >= fromUtc
                && l.SaleReturn.ProcessedOn <= now
                && l.SaleReturn.Status == SaleReturnStatus.Completed)
            .GroupBy(l => new { l.SaleReturn!.StoreId, l.ProductId, l.ProductVariantId })
            .Select(g => new MovementQtyRow(g.Key.StoreId, g.Key.ProductId, g.Key.ProductVariantId, g.Sum(x => x.QuantityReturned)))
            .ToListAsync(ct);

        var lastSales = await db.SaleLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId)
                && l.Sale != null
                && storeIds.Contains(l.Sale.StoreId)
                && l.Sale.CompletedOn >= deadStockFromUtc
                && l.Sale.CompletedOn <= now
                && l.Sale.Status != SaleStatus.Voided)
            .GroupBy(l => new { l.Sale!.StoreId, l.ProductId, l.ProductVariantId })
            .Select(g => new LastSoldRow(g.Key.StoreId, g.Key.ProductId, g.Key.ProductVariantId, g.Max(x => x.Sale!.CompletedOn)))
            .ToListAsync(ct);

        var incomingPurchases = await db.PurchaseLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId)
                && l.Purchase != null
                && storeIds.Contains(l.Purchase.StoreId)
                && l.Purchase.Status == PurchaseStatus.Draft)
            .GroupBy(l => new { l.Purchase!.StoreId, l.ProductId, l.ProductVariantId })
            .Select(g => new MovementQtyRow(g.Key.StoreId, g.Key.ProductId, g.Key.ProductVariantId, g.Sum(x => x.Quantity)))
            .ToListAsync(ct);

        var incomingTransfers = await db.StockTransferLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId)
                && l.StockTransfer != null
                && storeIds.Contains(l.StockTransfer.DestinationStoreId)
                && l.StockTransfer.Status != StockTransferStatus.Cancelled
                && l.StockTransfer.Status != StockTransferStatus.Received)
            .GroupBy(l => new { StoreId = l.StockTransfer!.DestinationStoreId, l.ProductId, l.ProductVariantId })
            .Select(g => new MovementQtyRow(
                g.Key.StoreId,
                g.Key.ProductId,
                g.Key.ProductVariantId,
                g.Sum(x => x.QuantityRequested - x.QuantityReceived)))
            .ToListAsync(ct);

        var supplierMap = await GetLastSupplierMapAsync(productIds, ct);

        var stockMap = stockRows.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId));
        var saleMap = saleQty.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId), v => v.Quantity);
        var returnMap = returnedQty.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId), v => v.Quantity);
        var lastSoldMap = lastSales.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId), v => (DateTime?)v.LastSoldOn);
        var incomingPurchaseMap = incomingPurchases.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId), v => v.Quantity);
        var incomingTransferMap = incomingTransfers.ToDictionary(k => Key(k.StoreId, k.ProductId, k.ProductVariantId), v => Math.Max(0, v.Quantity));

        var preliminary = new List<ReorderSuggestionDto>();
        foreach (var candidate in candidateKeys)
        {
            if (!productMap.TryGetValue(candidate.ProductId, out var product))
            {
                continue;
            }

            var key = Key(candidate.StoreId, candidate.ProductId, candidate.ProductVariantId);
            var current = stockMap.TryGetValue(key, out var stock) ? stock.QuantityOnHand : 0m;
            var sold = saleMap.GetValueOrDefault(key);
            var returned = returnMap.GetValueOrDefault(key);
            var netSold = Math.Max(0, sold - returned);
            var avgDaily = query.LookbackDays <= 0 ? 0 : netSold / query.LookbackDays;
            var incomingPurchase = incomingPurchaseMap.GetValueOrDefault(key);
            var incomingTransfer = incomingTransferMap.GetValueOrDefault(key);
            var lastSoldOn = lastSoldMap.GetValueOrDefault(key);
            decimal? daysLeft = avgDaily <= 0
                ? null
                : Math.Round(current / avgDaily, 1); var variantName = stock?.VariantName ?? candidate.VariantName;
            var variantSku = stock?.VariantSku ?? candidate.VariantSku;
            var cost = stock?.VariantCost ?? product.CostPrice;
            if (cost <= 0)
            {
                cost = product.CostPrice;
            }

            var leadDemand = avgDaily * query.LeadTimeDays;
            var safetyStock = Math.Max(product.ReorderLevel, avgDaily * query.SafetyStockDays);
            var targetStock = leadDemand + safetyStock;
            var rawNeed = targetStock - current - incomingPurchase - incomingTransfer;

            var suggestedNeed = rawNeed > 0 ? CeilingQty(rawNeed) : 0m;
            if (suggestedNeed <= 0 && product.ReorderLevel > 0 && current <= product.ReorderLevel)
            {
                suggestedNeed = CeilingQty(product.ReorderLevel - current - incomingPurchase - incomingTransfer);
            }

            var isDeadStock = current > 0 && lastSoldOn is null && netSold <= 0;
            if (isDeadStock)
            {
                suggestedNeed = 0;
            }

            var severity = GetSeverity(current, product.ReorderLevel, daysLeft, query.LeadTimeDays, suggestedNeed, isDeadStock);
            var isStockout = current <= 0;
            var isStockoutSoon = daysLeft is not null && daysLeft <= query.LeadTimeDays;
            var isHealthy = suggestedNeed <= 0 && !isDeadStock && !isStockout && !isStockoutSoon && current > product.ReorderLevel;

            if (isHealthy && !query.IncludeHealthy)
            {
                continue;
            }

            if (isDeadStock && !query.IncludeDeadStock)
            {
                continue;
            }

            var supplier = supplierMap.GetValueOrDefault(candidate.ProductId);
            preliminary.Add(new ReorderSuggestionDto
            {
                StoreId = candidate.StoreId,
                StoreName = storeNameMap.GetValueOrDefault(candidate.StoreId, string.Empty),
                ProductId = candidate.ProductId,
                ProductVariantId = candidate.ProductVariantId,
                Sku = variantSku ?? product.Sku,
                ProductName = product.Name,
                VariantName = variantName,
                CategoryName = product.CategoryName,
                BrandName = product.BrandName,
                CurrentStock = current,
                ReorderLevel = product.ReorderLevel,
                QuantitySold = sold,
                QuantityReturned = returned,
                NetQuantitySold = netSold,
                AverageDailySales = avgDaily,
                DaysOfStockLeft = daysLeft,
                LastSoldOn = lastSoldOn,
                LeadTimeDemand = leadDemand,
                SafetyStock = safetyStock,
                TargetStock = targetStock,
                IncomingPurchaseQuantity = incomingPurchase,
                IncomingTransferQuantity = incomingTransfer,
                SuggestedPurchaseQuantity = suggestedNeed,
                SuggestedTransferQuantity = 0,
                SuggestedSupplierId = supplier?.SupplierId,
                SuggestedSupplierName = supplier?.SupplierName,
                LastUnitCost = supplier?.UnitCost > 0 ? supplier.UnitCost : Math.Max(0, cost),
                Severity = severity,
                IsDeadStock = isDeadStock,
                IsStockout = isStockout,
                IsStockoutSoon = isStockoutSoon,
                IsHealthy = isHealthy
            });
        }

        if (query.PreferTransfers)
        {
            ApplyTransferRecommendations(preliminary, query);
        }

        foreach (var row in preliminary)
        {
            row.EstimatedPurchaseCost = row.SuggestedPurchaseQuantity * row.LastUnitCost;
            row.ActionType = GetActionType(row);
            row.Reason = BuildReason(row, query);
        }

        var results = preliminary
            .Where(r => query.IncludeHealthy || !r.IsHealthy)
            .OrderBy(r => SeverityRank(r.Severity))
            .ThenByDescending(r => r.EstimatedPurchaseCost)
            .ThenBy(r => r.StoreName)
            .ThenBy(r => r.Sku)
            .Take(query.Take <= 0 ? 500 : query.Take)
            .ToList();

        return new ReorderDashboardDto
        {
            Suggestions = results,
            PurchaseSuggestions = results.Where(r => r.SuggestedPurchaseQuantity > 0).ToList(),
            TransferSuggestions = results.Where(r => r.SuggestedTransferQuantity > 0 && r.SuggestedSourceStoreId is not null).ToList(),
            DeadStockWarnings = results.Where(r => r.IsDeadStock).ToList(),
            Summary = new ReorderSummaryDto
            {
                TotalSuggestions = results.Count,
                CriticalCount = results.Count(r => r.Severity == "Critical"),
                PurchaseCount = results.Count(r => r.SuggestedPurchaseQuantity > 0),
                TransferCount = results.Count(r => r.SuggestedTransferQuantity > 0 && r.SuggestedSourceStoreId is not null),
                DeadStockCount = results.Count(r => r.IsDeadStock),
                StockoutCount = results.Count(r => r.IsStockout),
                StockoutSoonCount = results.Count(r => r.IsStockoutSoon),
                EstimatedPurchaseValue = results.Sum(r => r.EstimatedPurchaseCost),
                EstimatedTransferQuantity = results.Sum(r => r.SuggestedTransferQuantity)
            }
        };
    }

    private async Task<Dictionary<Guid, SupplierSuggestionRow>> GetLastSupplierMapAsync(HashSet<Guid> productIds, CancellationToken ct)
    {
        var rows = await db.PurchaseLines.AsNoTracking()
            .Where(l => productIds.Contains(l.ProductId) && l.Purchase != null && l.Purchase.Supplier != null)
            .Select(l => new
            {
                l.ProductId,
                l.UnitCost,
                PurchasedOn = l.Purchase!.ReceivedOn ?? l.Purchase.OrderedOn ?? l.Purchase.CreatedOn,
                SupplierId = l.Purchase.SupplierId,
                SupplierName = l.Purchase.Supplier!.Name
            })
            .OrderByDescending(x => x.PurchasedOn)
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.ProductId)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var latest = g.First();
                    return new SupplierSuggestionRow(latest.SupplierId, latest.SupplierName, latest.UnitCost);
                });
    }

    private static void ApplyTransferRecommendations(List<ReorderSuggestionDto> rows, ReorderQueryDto query)
    {
        var byProduct = rows.GroupBy(r => new { r.ProductId, r.ProductVariantId });
        foreach (var group in byProduct)
        {
            var candidates = group.ToList();
            foreach (var target in candidates.Where(r => r.SuggestedPurchaseQuantity > 0 && !r.IsDeadStock))
            {
                var source = candidates
                    .Where(r => r.StoreId != target.StoreId && r.CurrentStock > 0)
                    .Select(r => new
                    {
                        Row = r,
                        Spare = CalculateSpareStock(r, query)
                    })
                    .Where(x => x.Spare > 0)
                    .OrderByDescending(x => x.Spare)
                    .ThenByDescending(x => x.Row.CurrentStock)
                    .FirstOrDefault();

                if (source is null)
                {
                    continue;
                }

                var transferQty = Math.Min(target.SuggestedPurchaseQuantity, CeilingQty(source.Spare));
                if (transferQty <= 0)
                {
                    continue;
                }

                target.SuggestedSourceStoreId = source.Row.StoreId;
                target.SuggestedSourceStoreName = source.Row.StoreName;
                target.SuggestedSourceAvailableQuantity = source.Spare;
                target.SuggestedTransferQuantity = transferQty;
                target.SuggestedPurchaseQuantity = Math.Max(0, target.SuggestedPurchaseQuantity - transferQty);
            }
        }
    }

    private static decimal CalculateSpareStock(ReorderSuggestionDto row, ReorderQueryDto query)
    {
        if (row.IsDeadStock)
        {
            return row.CurrentStock;
        }

        var target = Math.Max(row.ReorderLevel, row.AverageDailySales * (query.LeadTimeDays + query.SafetyStockDays));
        return Math.Max(0, row.CurrentStock - target);
    }

    private static string GetActionType(ReorderSuggestionDto row)
    {
        if (row.IsDeadStock)
        {
            return "Do not reorder";
        }

        if (row.SuggestedTransferQuantity > 0 && row.SuggestedPurchaseQuantity > 0)
        {
            return "Transfer + Purchase";
        }

        if (row.SuggestedTransferQuantity > 0)
        {
            return "Transfer";
        }

        if (row.SuggestedPurchaseQuantity > 0)
        {
            return "Purchase";
        }

        return row.IsHealthy ? "Healthy" : "Review";
    }

    private static string BuildReason(ReorderSuggestionDto row, ReorderQueryDto query)
    {
        if (row.IsDeadStock)
        {
            return $"Dead/slow stock: {row.CurrentStock:0.###} on hand and no sale in the last {query.DeadStockDays} days. Do not purchase more yet.";
        }

        if (row.IsStockout)
        {
            return "Stockout. Reorder or transfer urgently.";
        }

        if (row.DaysOfStockLeft is not null && row.DaysOfStockLeft <= query.LeadTimeDays)
        {
            return $"Only {row.DaysOfStockLeft:0.#} day(s) of stock left based on average sales. Supplier lead time is {query.LeadTimeDays} day(s).";
        }

        if (row.CurrentStock <= row.ReorderLevel)
        {
            return $"Current stock {row.CurrentStock:0.###} is at/below reorder level {row.ReorderLevel:0.###}.";
        }

        if (row.SuggestedTransferQuantity > 0)
        {
            return $"Transfer available from {row.SuggestedSourceStoreName}; purchase only remaining quantity if needed.";
        }

        if (row.SuggestedPurchaseQuantity > 0)
        {
            return "Purchase suggestion calculated from sales velocity, lead time, safety stock, current stock, and incoming stock.";
        }

        return "No immediate action required.";
    }

    private static IReadOnlyList<CandidateKey> BuildCandidateKeys(
        IReadOnlyList<StoreAssignmentRow> assignments,
        IReadOnlyList<ProductRow> products,
        IReadOnlyList<StockRow> stocks)
    {
        var result = new Dictionary<string, CandidateKey>();
        var productMap = products.ToDictionary(p => p.Id);
        var assignedPairs = assignments
            .Select(a => (a.StoreId, a.ProductId))
            .ToHashSet();

        foreach (var stock in stocks)
        {
            if (!assignedPairs.Contains((stock.StoreId, stock.ProductId)))
            {
                continue;
            }

            result[Key(stock.StoreId, stock.ProductId, stock.ProductVariantId)] = new CandidateKey(
                stock.StoreId,
                stock.ProductId,
                stock.ProductVariantId,
                stock.VariantName,
                stock.VariantSku);
        }

        foreach (var assignment in assignments)
        {
            if (!productMap.TryGetValue(assignment.ProductId, out var product) || product.ReorderLevel <= 0)
            {
                continue;
            }

            var key = Key(assignment.StoreId, assignment.ProductId, null);
            result.TryAdd(key, new CandidateKey(assignment.StoreId, assignment.ProductId, null, null, null));
        }

        return result.Values.ToList();
    }

    private static ReorderQueryDto Normalize(ReorderQueryDto query)
    {
        query.LookbackDays = Math.Clamp(query.LookbackDays <= 0 ? 30 : query.LookbackDays, 7, 365);
        query.LeadTimeDays = Math.Clamp(query.LeadTimeDays <= 0 ? 7 : query.LeadTimeDays, 1, 365);
        query.SafetyStockDays = Math.Clamp(query.SafetyStockDays < 0 ? 3 : query.SafetyStockDays, 0, 365);
        query.DeadStockDays = Math.Clamp(query.DeadStockDays <= 0 ? 60 : query.DeadStockDays, 15, 730);
        query.Take = Math.Clamp(query.Take <= 0 ? 500 : query.Take, 50, 2000);
        return query;
    }

    private static string GetSeverity(decimal current, decimal reorderLevel, decimal? daysLeft, int leadTimeDays, decimal suggestedQty, bool isDeadStock)
    {
        if (isDeadStock)
        {
            return "Info";
        }

        if (current <= 0)
        {
            return "Critical";
        }

        if (daysLeft is not null && daysLeft <= Math.Max(1, leadTimeDays / 2m))
        {
            return "Critical";
        }

        if (suggestedQty > 0 || current <= reorderLevel || (daysLeft is not null && daysLeft <= leadTimeDays))
        {
            return "Warning";
        }

        return "Info";
    }

    private static int SeverityRank(string severity) => severity switch
    {
        "Critical" => 0,
        "Warning" => 1,
        _ => 2
    };

    private static decimal CeilingQty(decimal value) => value <= 0 ? 0 : Math.Ceiling(value * 1000m) / 1000m;
    private static decimal RoundQty(decimal value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);
    private static string Key(Guid storeId, Guid productId, Guid? variantId) => $"{storeId:N}|{productId:N}|{variantId?.ToString("N") ?? "product"}";

    private sealed record StoreRow(Guid Id, string Name);
    private sealed record StoreAssignmentRow(Guid StoreId, Guid ProductId);
    private sealed record ProductRow(Guid Id, string Sku, string Name, string? CategoryName, string? BrandName, decimal CostPrice, decimal SellPrice, decimal ReorderLevel);
    private sealed record StockRow(Guid StoreId, Guid ProductId, Guid? ProductVariantId, string? VariantName, string? VariantSku, decimal? VariantCost, decimal QuantityOnHand);
    private sealed record CandidateKey(Guid StoreId, Guid ProductId, Guid? ProductVariantId, string? VariantName, string? VariantSku);
    private sealed record MovementQtyRow(Guid StoreId, Guid ProductId, Guid? ProductVariantId, decimal Quantity);
    private sealed record LastSoldRow(Guid StoreId, Guid ProductId, Guid? ProductVariantId, DateTime LastSoldOn);
    private sealed record SupplierSuggestionRow(Guid SupplierId, string SupplierName, decimal UnitCost);
}
