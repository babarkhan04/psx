using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Sales;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed partial class ReportService(AppDbContext db, DbContextGate gate, IStoreAccessService storeAccess) : IReportService
{
    private IReadOnlyList<Guid> reportStoreIds = [];
    public Task<ReportsPageDto> GetReportsAsync(ReportQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetReportsCoreAsync(query, ct), ct);

    public Task<byte[]> ExportReportsWorkbookAsync(ReportQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ExportReportsWorkbookCoreAsync(query, ct), ct);

    private async Task<ReportsPageDto> GetReportsCoreAsync(ReportQueryDto query, CancellationToken ct)
    {
        reportStoreIds = await storeAccess.GetAllowedStoreIdsAsync(ct);
        if (query.StoreId is { } requestedStore)
            await storeAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
        var fromUtc = query.FromUtc ?? DateTime.UtcNow.Date.AddDays(-29);
        var toUtc = query.ToUtc ?? DateTime.UtcNow.Date.AddDays(1);

        if (toUtc <= fromUtc)
        {
            toUtc = fromUtc.AddDays(1);
        }

        var stores = await db.Stores.AsNoTracking().Where(store => reportStoreIds.Contains(store.Id)).ToListAsync(ct);
        var storesById = stores.ToDictionary(s => s.Id, s => FormatStore(s));

        var registers = await db.Registers.AsNoTracking().Where(register => reportStoreIds.Contains(register.StoreId)).ToListAsync(ct);
        var registersById = registers.ToDictionary(r => r.Id, r => r);

        var usersById = await db.Users.AsNoTracking()
            .Select(u => new { u.Id, Name = string.IsNullOrWhiteSpace(u.DisplayName) ? (u.Email ?? u.UserName ?? u.Id) : u.DisplayName })
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var salesQuery = db.Sales.AsNoTracking().Where(sale => reportStoreIds.Contains(sale.StoreId))
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Where(s => s.CompletedOn >= fromUtc && s.CompletedOn < toUtc);

        if (query.StoreId is { } storeId)
        {
            salesQuery = salesQuery.Where(s => s.StoreId == storeId);
        }

        var sales = await salesQuery.ToListAsync(ct);
        var activeSales = sales.Where(s => s.Status != SaleStatus.Voided).ToList();
        var voidSales = sales.Where(s => s.Status == SaleStatus.Voided).ToList();

        var returnsQuery = db.SaleReturns.AsNoTracking().Where(item => reportStoreIds.Contains(item.StoreId))
            .Include(r => r.Lines).ThenInclude(line => line.OriginalSaleLine)
            .Where(r => r.ProcessedOn >= fromUtc && r.ProcessedOn < toUtc && r.Status == SaleReturnStatus.Completed);

        if (query.StoreId is { } storeId2)
        {
            returnsQuery = returnsQuery.Where(r => r.StoreId == storeId2);
        }

        var returns = await returnsQuery.ToListAsync(ct);

        var products = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .ToListAsync(ct);
        var productsById = products.ToDictionary(p => p.Id, p => p);

        var stockRows = await BuildCurrentStockAsync(query.StoreId, productsById, storesById, ct);
        var stockLedgerRows = await BuildStockLedgerAsync(query.StoreId, fromUtc, toUtc, storesById, ct);
        var stockMovementRows = BuildMovementSummary(stockLedgerRows);
        var purchaseRows = await BuildPurchaseRegisterAsync(query.StoreId, fromUtc, toUtc, storesById, ct);
        var stockTransferRows = await BuildStockTransferReportAsync(query.StoreId, fromUtc, toUtc, storesById, ct);

        var saleLines = activeSales.SelectMany(s => s.Lines.Select(l => new SaleLineWithHeader(s, l))).ToList();
        var returnLines = returns.SelectMany(r => r.Lines.Select(l => new ReturnLineWithHeader(r, l))).ToList();

        var productStats = BuildProductRows(saleLines, returnLines, productsById);
        var grossProfit = productStats.Sum(p => p.GrossProfit);

        var grossSales = activeSales.Sum(s => s.TotalAmount);
        var returnTotal = returns.Sum(r => r.TotalAmount);
        var netSales = grossSales - returnTotal;
        var discountTotal = activeSales.Sum(s => s.DiscountAmount);
        var taxCollected = activeSales.Sum(s => s.TaxAmount) - returns.Sum(r => r.TaxAmount);
        var payments = activeSales.SelectMany(s => s.Payments).ToList();
        var paymentTotal = payments.Sum(p => p.Amount);

        var summary = new SalesReportSummaryDto
        {
            GrossSales = grossSales,
            ReturnsTotal = returnTotal,
            NetSales = netSales,
            TaxCollected = taxCollected,
            DiscountTotal = discountTotal,
            GrossProfit = grossProfit,
            MarginPercent = SafePercent(grossProfit, netSales),
            TransactionCount = activeSales.Count,
            ReturnCount = returns.Count,
            VoidCount = voidSales.Count,
            AverageBasket = activeSales.Count == 0 ? 0 : netSales / activeSales.Count,
            CashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount),
            CardSales = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount),
            OtherSales = payments.Where(p => p.Method == PaymentMethod.Other).Sum(p => p.Amount),
            CreditSales = payments.Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount)
        };

        return new ReportsPageDto
        {
            SalesRegister = BuildSalesRegister(sales, returns, storesById, usersById),
            PurchaseRegister = purchaseRows,
            StockLedger = stockLedgerRows,
            SlowMovingStock = BuildSlowMovingStock(stockRows, activeSales, fromUtc, toUtc),
            WeekdaySales = BuildWeekdaySales(activeSales, returns, fromUtc, toUtc),
            ProductPerformance = productStats.OrderByDescending(p => p.Revenue).ToList(),
            SalesSummary = summary,
            DailySales = BuildDailySales(activeSales, voidSales, returns, productStats),
            SalesByStore = BuildStoreSales(activeSales, returns, productStats, storesById),
            SalesByRegister = BuildRegisterSales(activeSales, registersById, storesById),
            SalesByCashier = BuildCashierSales(activeSales, voidSales, returns, usersById),
            HourlySales = BuildHourlySales(activeSales, returns),
            PaymentMix = BuildPaymentMix(payments, paymentTotal),
            TopProducts = productStats.OrderByDescending(p => p.Revenue).Take(50).ToList(),
            SalesByCategory = BuildCategorySales(productStats),
            SalesByBrand = BuildBrandSales(productStats),
            Discounts = BuildDiscountRows(activeSales, storesById, usersById),
            Voids = BuildVoidRows(voidSales, storesById, usersById),
            Returns = BuildReturnRows(returns, storesById, usersById),
            CurrentStock = stockRows,
            StockMovements = stockMovementRows,
            StockTransfers = stockTransferRows,
            Tax = BuildTaxReport(saleLines, taxCollected, activeSales),
            Purchases = BuildPurchaseSummary(purchaseRows),
            Customers = await BuildCustomerReportAsync(activeSales, returns, ct),
            Shifts = await BuildShiftReportAsync(query.StoreId, fromUtc, toUtc, registersById, storesById, usersById, ct)
        };
    }

    private async Task<IReadOnlyList<CurrentStockRowDto>> BuildCurrentStockAsync(
        Guid? storeId,
        IReadOnlyDictionary<Guid, Product> productsById,
        IReadOnlyDictionary<Guid, string> storesById,
        CancellationToken ct)
    {
        var stockQuery = db.Stocks.AsNoTracking().Include(stock => stock.ProductVariant).Where(stock => reportStoreIds.Contains(stock.StoreId));
        if (storeId is { } id)
        {
            stockQuery = stockQuery.Where(s => s.StoreId == id);
        }

        var stocks = await stockQuery.ToListAsync(ct);
        return stocks.Select(s =>
            {
                productsById.TryGetValue(s.ProductId, out var product);
                var cost = s.ProductVariant?.CostPrice ?? product?.CostPrice ?? 0m;
                var reorder = product?.ReorderLevel ?? 0m;
                return new CurrentStockRowDto
                {
                    StoreId = s.StoreId,
                    ProductId = s.ProductId,
                    ProductVariantId = s.ProductVariantId,
                    StoreName = storesById.TryGetValue(s.StoreId, out var storeName) ? storeName : "Unknown store",
                    Sku = s.ProductVariant?.Sku ?? product?.Sku ?? string.Empty,
                    ProductName = s.ProductVariant is null ? product?.Name ?? "Unknown product" : $"{product?.Name} · {s.ProductVariant.Name}",
                    CategoryName = product?.Category?.Name ?? "Uncategorized",
                    BrandName = product?.Brand?.Name ?? "No brand",
                    QuantityOnHand = s.QuantityOnHand,
                    ReorderLevel = reorder,
                    CostPrice = cost,
                    StockValue = s.QuantityOnHand * cost,
                    IsLowStock = reorder > 0 && s.QuantityOnHand > 0 && s.QuantityOnHand <= reorder,
                    IsOutOfStock = s.QuantityOnHand <= 0
                };
            })
            .OrderByDescending(s => s.IsOutOfStock)
            .ThenByDescending(s => s.IsLowStock)
            .ThenBy(s => s.StoreName)
            .ThenBy(s => s.ProductName)
            .ToList();
    }

    private async Task<IReadOnlyList<StockTransferReportRowDto>> BuildStockTransferReportAsync(
        Guid? storeId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyDictionary<Guid, string> storesById,
        CancellationToken ct)
    {
        var query = db.StockTransfers.AsNoTracking().Where(transfer => reportStoreIds.Contains(transfer.StoreId) || reportStoreIds.Contains(transfer.DestinationStoreId))
            .Include(t => t.Lines)
            .Where(t => t.RequestedOn >= fromUtc && t.RequestedOn < toUtc);

        if (storeId is { } id)
        {
            query = query.Where(t => t.StoreId == id || t.DestinationStoreId == id);
        }

        var transfers = await query.ToListAsync(ct);
        return transfers.Select(t => new StockTransferReportRowDto
            {
                TransferNumber = t.TransferNumber,
                SourceStoreName = storesById.TryGetValue(t.StoreId, out var source) ? source : "Unknown source",
                DestinationStoreName = storesById.TryGetValue(t.DestinationStoreId, out var dest) ? dest : "Unknown destination",
                Status = t.Status.ToString(),
                RequestedOn = t.RequestedOn,
                QuantityRequested = t.Lines.Sum(l => l.QuantityRequested),
                QuantityDispatched = t.Lines.Sum(l => l.QuantityDispatched),
                QuantityReceived = t.Lines.Sum(l => l.QuantityReceived)
            })
            .OrderByDescending(t => t.RequestedOn)
            .ToList();
    }

    private static IReadOnlyList<ProductSalesRowDto> BuildProductRows(
        IReadOnlyList<SaleLineWithHeader> saleLines,
        IReadOnlyList<ReturnLineWithHeader> returnLines,
        IReadOnlyDictionary<Guid, Product> productsById)
    {
        var allocations = saleLines.Select(item => item.Sale).DistinctBy(sale => sale.Id)
            .ToDictionary(sale => sale.Id, SaleFinancials.GetRefundableLines);
        var keys = saleLines.Select(item => (item.Line.ProductId, item.Line.Sku))
            .Concat(returnLines.Select(item => (item.Line.ProductId, item.Line.Sku))).Distinct();
        return keys.Select(key =>
        {
            var sold = saleLines.Where(item => item.Line.ProductId == key.ProductId && item.Line.Sku == key.Sku).ToList();
            var returned = returnLines.Where(item => item.Line.ProductId == key.ProductId && item.Line.Sku == key.Sku).ToList();
            var quantitySold = sold.Sum(item => item.Line.Quantity);
            var quantityReturned = returned.Sum(item => item.Line.QuantityReturned);
            var revenue = sold.Sum(item => allocations[item.Sale.Id][item.Line.Id].Total) - returned.Sum(item => item.Line.LineTotal);
            var tax = sold.Sum(item => allocations[item.Sale.Id][item.Line.Id].Tax) - returned.Sum(item => item.Line.TaxAmount);
            var cost = sold.Sum(item => item.Line.Quantity * item.Line.CostPrice) - returned.Where(item => item.Line.Restock)
                .Sum(item => item.Line.QuantityReturned * (item.Line.OriginalSaleLine?.CostPrice ?? 0m));
            var profit = revenue - tax - cost;
            productsById.TryGetValue(key.ProductId, out var product);
            return new ProductSalesRowDto
            {
                ProductId = key.ProductId,
                Sku = key.Sku,
                ProductName = sold.OrderByDescending(item => item.Sale.CompletedOn).FirstOrDefault()?.Line.ProductName
                    ?? returned.FirstOrDefault()?.Line.ProductName ?? product?.Name ?? "Unknown product",
                CategoryName = product?.Category?.Name ?? "Uncategorized",
                BrandName = product?.Brand?.Name ?? "No brand",
                QuantitySold = quantitySold,
                QuantityReturned = quantityReturned,
                NetQuantity = quantitySold - quantityReturned,
                Revenue = revenue,
                Cost = cost,
                GrossProfit = profit,
                MarginPercent = SafePercent(profit, revenue),
                DiscountAmount = sold.Sum(item => item.Line.DiscountAmount)
            };
        }).Where(item => item.QuantitySold != 0 || item.QuantityReturned != 0 || item.Revenue != 0).ToList();
    }

    private static IReadOnlyList<DailySalesRowDto> BuildDailySales(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyList<Sale> voidSales,
        IReadOnlyList<SaleReturn> returns,
        IReadOnlyList<ProductSalesRowDto> productStats)
    {
        var dates = activeSales.Select(s => s.CompletedOn.Date)
            .Concat(voidSales.Select(s => s.CompletedOn.Date))
            .Concat(returns.Select(r => r.ProcessedOn.Date))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        return dates.Select(date =>
        {
            var daySales = activeSales.Where(s => s.CompletedOn.Date == date).ToList();
            var dayReturns = returns.Where(r => r.ProcessedOn.Date == date).ToList();
            var dayVoidCount = voidSales.Count(s => s.CompletedOn.Date == date);
            var gross = daySales.Sum(s => s.TotalAmount);
            var returned = dayReturns.Sum(r => r.TotalAmount);
            var net = gross - returned;
            var dayLines = daySales.SelectMany(s => s.Lines).ToList();
            var cost = dayLines.Sum(l => l.Quantity * l.CostPrice);
            var returnedCost = dayReturns.SelectMany(item => item.Lines).Where(line => line.Restock)
                .Sum(line => line.QuantityReturned * (line.OriginalSaleLine?.CostPrice ?? 0m));
            var netTax = daySales.Sum(sale => sale.TaxAmount) - dayReturns.Sum(item => item.TaxAmount);
            var profit = net - netTax - cost + returnedCost;
            return new DailySalesRowDto
            {
                Date = date,
                Transactions = daySales.Count,
                ReturnsCount = dayReturns.Count,
                VoidCount = dayVoidCount,
                GrossSales = gross,
                Discounts = daySales.Sum(s => s.DiscountAmount),
                Returns = returned,
                NetSales = net,
                Tax = daySales.Sum(s => s.TaxAmount) - dayReturns.Sum(r => r.TaxAmount),
                GrossProfit = profit,
                MarginPercent = SafePercent(profit, net)
            };
        }).ToList();
    }

    private static IReadOnlyList<StoreSalesRowDto> BuildStoreSales(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyList<SaleReturn> returns,
        IReadOnlyList<ProductSalesRowDto> productStats,
        IReadOnlyDictionary<Guid, string> storesById)
    {
        return activeSales
            .GroupBy(s => s.StoreId)
            .Select(g =>
            {
                var storeReturns = returns.Where(r => r.StoreId == g.Key).Sum(r => r.TotalAmount);
                var gross = g.Sum(s => s.TotalAmount);
                var net = gross - storeReturns;
                var lineCost = g.SelectMany(s => s.Lines).Sum(l => l.Quantity * l.CostPrice);
                var returnRows = returns.Where(item => item.StoreId == g.Key).ToList();
                var returnedCost = returnRows.SelectMany(item => item.Lines).Where(line => line.Restock)
                    .Sum(line => line.QuantityReturned * (line.OriginalSaleLine?.CostPrice ?? 0m));
                var tax = g.Sum(sale => sale.TaxAmount) - returnRows.Sum(item => item.TaxAmount);
                var profit = net - tax - lineCost + returnedCost;
                return new StoreSalesRowDto
                {
                    StoreId = g.Key,
                    StoreName = storesById.TryGetValue(g.Key, out var storeName) ? storeName : "Unknown store",
                    Transactions = g.Count(),
                    GrossSales = gross,
                    Returns = storeReturns,
                    NetSales = net,
                    Discounts = g.Sum(s => s.DiscountAmount),
                    GrossProfit = profit,
                    MarginPercent = SafePercent(profit, net),
                    AverageBasket = g.Count() == 0 ? 0 : net / g.Count()
                };
            })
            .OrderByDescending(s => s.NetSales)
            .ToList();
    }

    private static IReadOnlyList<RegisterSalesRowDto> BuildRegisterSales(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyDictionary<Guid, Register> registersById,
        IReadOnlyDictionary<Guid, string> storesById)
    {
        return activeSales.GroupBy(s => s.RegisterId)
            .Select(g =>
            {
                registersById.TryGetValue(g.Key, out var register);
                var payments = g.SelectMany(s => s.Payments).ToList();
                return new RegisterSalesRowDto
                {
                    RegisterId = g.Key,
                    RegisterName = register is null ? "Unknown register" : register.Code + " · " + register.Name,
                    StoreName = register is not null && storesById.TryGetValue(register.StoreId, out var storeName) ? storeName : string.Empty,
                    Transactions = g.Count(),
                    NetSales = g.Sum(s => s.TotalAmount),
                    CashSales = payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount),
                    CardSales = payments.Where(p => p.Method == PaymentMethod.Card).Sum(p => p.Amount),
                    CreditSales = payments.Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount)
                };
            })
            .OrderByDescending(r => r.NetSales)
            .ToList();
    }

    private static IReadOnlyList<CashierSalesRowDto> BuildCashierSales(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyList<Sale> voidSales,
        IReadOnlyList<SaleReturn> returns,
        IReadOnlyDictionary<string, string> usersById)
    {
        return activeSales.GroupBy(s => s.CashierUserId)
            .Select(g =>
            {
                var returned = returns.Where(r => r.ProcessedByUserId == g.Key).Sum(r => r.TotalAmount);
                var gross = g.Sum(s => s.TotalAmount);
                var net = gross - returned;
                return new CashierSalesRowDto
                {
                    CashierUserId = g.Key,
                    CashierName = GetUserName(usersById, g.Key),
                    Transactions = g.Count(),
                    VoidCount = voidSales.Count(s => s.CashierUserId == g.Key),
                    ReturnCount = returns.Count(r => r.ProcessedByUserId == g.Key),
                    GrossSales = gross,
                    Discounts = g.Sum(s => s.DiscountAmount),
                    Returns = returned,
                    NetSales = net,
                    AverageBasket = g.Count() == 0 ? 0 : net / g.Count()
                };
            })
            .OrderByDescending(c => c.NetSales)
            .ToList();
    }

    private static IReadOnlyList<HourlySalesRowDto> BuildHourlySales(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyList<SaleReturn> returns)
    {
        return Enumerable.Range(0, 24).Select(hour =>
        {
            var hourSales = activeSales.Where(s => s.CompletedOn.Hour == hour).ToList();
            var hourReturns = returns.Where(r => r.ProcessedOn.Hour == hour).Sum(r => r.TotalAmount);
            var net = hourSales.Sum(s => s.TotalAmount) - hourReturns;
            return new HourlySalesRowDto
            {
                Hour = hour,
                HourLabel = $"{hour:00}:00 - {hour:00}:59",
                Transactions = hourSales.Count,
                NetSales = net,
                AverageBasket = hourSales.Count == 0 ? 0 : net / hourSales.Count
            };
        }).Where(h => h.Transactions > 0 || h.NetSales != 0).ToList();
    }

    private static IReadOnlyList<PaymentMethodRowDto> BuildPaymentMix(IReadOnlyList<Payment> payments, decimal total)
    {
        return payments.GroupBy(p => p.Method)
            .Select(g => new PaymentMethodRowDto
            {
                Method = g.Key.ToString(),
                PaymentCount = g.Count(),
                Amount = g.Sum(p => p.Amount),
                PercentOfTotal = SafePercent(g.Sum(p => p.Amount), total)
            })
            .OrderByDescending(p => p.Amount)
            .ToList();
    }

    private static IReadOnlyList<CategorySalesRowDto> BuildCategorySales(IReadOnlyList<ProductSalesRowDto> products)
    {
        return products.GroupBy(p => string.IsNullOrWhiteSpace(p.CategoryName) ? "Uncategorized" : p.CategoryName)
            .Select(g =>
            {
                var revenue = g.Sum(p => p.Revenue);
                var profit = g.Sum(p => p.GrossProfit);
                return new CategorySalesRowDto
                {
                    CategoryName = g.Key,
                    QuantitySold = g.Sum(p => p.NetQuantity),
                    Revenue = revenue,
                    GrossProfit = profit,
                    MarginPercent = SafePercent(profit, revenue)
                };
            })
            .OrderByDescending(c => c.Revenue)
            .ToList();
    }

    private static IReadOnlyList<BrandSalesRowDto> BuildBrandSales(IReadOnlyList<ProductSalesRowDto> products)
    {
        return products.GroupBy(p => string.IsNullOrWhiteSpace(p.BrandName) ? "No brand" : p.BrandName)
            .Select(g =>
            {
                var revenue = g.Sum(p => p.Revenue);
                var profit = g.Sum(p => p.GrossProfit);
                return new BrandSalesRowDto
                {
                    BrandName = g.Key,
                    QuantitySold = g.Sum(p => p.NetQuantity),
                    Revenue = revenue,
                    GrossProfit = profit,
                    MarginPercent = SafePercent(profit, revenue)
                };
            })
            .OrderByDescending(b => b.Revenue)
            .ToList();
    }

    private static IReadOnlyList<DiscountReportRowDto> BuildDiscountRows(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyDictionary<Guid, string> storesById,
        IReadOnlyDictionary<string, string> usersById)
    {
        return activeSales.Select(s => new
            {
                Sale = s,
                LineDiscount = s.Lines.Sum(l => l.DiscountAmount),
                InvoiceDiscount = Math.Max(0m, s.DiscountAmount - s.Lines.Sum(line => line.DiscountAmount))
            })
            .Where(x => x.LineDiscount > 0 || x.InvoiceDiscount > 0)
            .Select(x => new DiscountReportRowDto
            {
                SaleNumber = x.Sale.SaleNumber,
                CompletedOn = x.Sale.CompletedOn,
                StoreName = storesById.TryGetValue(x.Sale.StoreId, out var storeName) ? storeName : "Unknown store",
                CashierName = GetUserName(usersById, x.Sale.CashierUserId),
                CustomerName = x.Sale.CustomerName,
                InvoiceDiscount = x.InvoiceDiscount,
                LineDiscount = x.LineDiscount,
                TotalDiscount = x.InvoiceDiscount + x.LineDiscount,
                Reason = x.Sale.DiscountReason ?? string.Join(", ", x.Sale.Lines.Where(l => !string.IsNullOrWhiteSpace(l.DiscountReason)).Select(l => l.DiscountReason).Distinct())
            })
            .OrderByDescending(d => d.CompletedOn)
            .ToList();
    }

    private static IReadOnlyList<VoidSaleReportRowDto> BuildVoidRows(
        IReadOnlyList<Sale> voidSales,
        IReadOnlyDictionary<Guid, string> storesById,
        IReadOnlyDictionary<string, string> usersById)
    {
        return voidSales.Select(s => new VoidSaleReportRowDto
            {
                SaleNumber = s.SaleNumber,
                CompletedOn = s.CompletedOn,
                StoreName = storesById.TryGetValue(s.StoreId, out var storeName) ? storeName : "Unknown store",
                CashierName = GetUserName(usersById, s.CashierUserId),
                CustomerName = s.CustomerName,
                TotalAmount = s.TotalAmount,
                Notes = s.Notes
            })
            .OrderByDescending(v => v.CompletedOn)
            .ToList();
    }

    private static IReadOnlyList<ReturnReportRowDto> BuildReturnRows(
        IReadOnlyList<SaleReturn> returns,
        IReadOnlyDictionary<Guid, string> storesById,
        IReadOnlyDictionary<string, string> usersById)
    {
        return returns.Select(r => new ReturnReportRowDto
            {
                ReturnNumber = r.ReturnNumber,
                ProcessedOn = r.ProcessedOn,
                StoreName = storesById.TryGetValue(r.StoreId, out var storeName) ? storeName : "Unknown store",
                ProcessedByName = GetUserName(usersById, r.ProcessedByUserId),
                CustomerName = r.CustomerName,
                RefundMethod = r.RefundMethod.ToString(),
                TotalAmount = r.TotalAmount,
                TaxAmount = r.TaxAmount,
                Notes = r.Notes
            })
            .OrderByDescending(r => r.ProcessedOn)
            .ToList();
    }

    private static TaxReportDto BuildTaxReport(
        IReadOnlyList<SaleLineWithHeader> saleLines,
        decimal taxCollected,
        IReadOnlyList<Sale> activeSales)
    {
        var byRate = saleLines
            .GroupBy(x => x.Line.TaxRate)
            .Select(g => new TaxRateRowDto
            {
                Rate = g.Key,
                TaxableAmount = g.Sum(x => x.Line.LineTotal - x.Line.TaxAmount),
                TaxAmount = g.Sum(x => x.Line.TaxAmount)
            })
            .OrderByDescending(t => t.TaxAmount)
            .ToList();

        return new TaxReportDto
        {
            TaxableSales = saleLines.Sum(x => x.Line.LineTotal - x.Line.TaxAmount),
            GstCollected = taxCollected,
            ExemptSales = activeSales.Where(s => s.TaxAmount == 0).Sum(s => s.Subtotal),
            ByRate = byRate
        };
    }

    private async Task<IReadOnlyList<CustomerReportRowDto>> BuildCustomerReportAsync(
        IReadOnlyList<Sale> activeSales,
        IReadOnlyList<SaleReturn> returns,
        CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking().ToListAsync(ct);
        var rows = customers.Select(c =>
            {
                var customerSales = activeSales.Where(s => s.CustomerId == c.Id).ToList();
                var customerReturns = returns.Where(r => r.CustomerId == c.Id).ToList();
                var spend = customerSales.Sum(s => s.TotalAmount) - customerReturns.Sum(r => r.TotalAmount);
                return new CustomerReportRowDto
                {
                    CustomerId = c.Id,
                    CustomerName = c.Name,
                    Phone = c.Phone,
                    InvoiceCount = customerSales.Count,
                    TotalSpend = spend,
                    Returns = customerReturns.Sum(r => r.TotalAmount),
                    Balance = c.Balance,
                    LastPurchaseOn = customerSales.Count == 0 ? null : customerSales.Max(s => s.CompletedOn),
                    AverageBasket = customerSales.Count == 0 ? 0 : spend / customerSales.Count
                };
            })
            .Where(c => c.InvoiceCount > 0 || c.Balance != 0)
            .OrderByDescending(c => c.TotalSpend)
            .Take(100)
            .ToList();

        return rows;
    }

    private async Task<IReadOnlyList<ShiftCashReportRowDto>> BuildShiftReportAsync(
        Guid? storeId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyDictionary<Guid, Register> registersById,
        IReadOnlyDictionary<Guid, string> storesById,
        IReadOnlyDictionary<string, string> usersById,
        CancellationToken ct)
    {
        var query = db.Shifts.AsNoTracking().Where(shift => reportStoreIds.Contains(shift.StoreId))
            .Where(s => s.OpenedOn >= fromUtc && s.OpenedOn < toUtc);

        if (storeId is { } id)
        {
            query = query.Where(s => s.StoreId == id);
        }

        var shifts = await query.ToListAsync(ct);
        return shifts.Select(s =>
            {
                registersById.TryGetValue(s.RegisterId, out var register);
                return new ShiftCashReportRowDto
                {
                    ShiftNumber = s.ShiftNumber,
                    StoreName = storesById.TryGetValue(s.StoreId, out var storeName) ? storeName : "Unknown store",
                    RegisterName = register is null ? "Unknown register" : register.Code + " · " + register.Name,
                    OpenedByName = GetUserName(usersById, s.OpenedByUserId),
                    OpenedOn = s.OpenedOn,
                    ClosedOn = s.ClosedOn,
                    Status = s.Status.ToString(),
                    OpeningFloat = s.OpeningFloat,
                    ExpectedCash = s.ExpectedCash,
                    CountedCash = s.CountedCash,
                    Variance = s.Variance
                };
            })
            .OrderByDescending(s => s.OpenedOn)
            .ToList();
    }

    private async Task<byte[]> ExportReportsWorkbookCoreAsync(ReportQueryDto query, CancellationToken ct)
    {
        var report = await GetReportsCoreAsync(query, ct);

        using var workbook = new XLWorkbook();
        AddSummaryWorksheet(workbook, query, report);
        AddDailySalesWorksheet(workbook, report);
        AddStoreSalesWorksheet(workbook, report);
        AddCashierSalesWorksheet(workbook, report);
        AddProductProfitWorksheet(workbook, report);
        AddPaymentWorksheet(workbook, report);
        AddDiscountWorksheet(workbook, report);
        AddVoidWorksheet(workbook, report);
        AddReturnWorksheet(workbook, report);
        AddInventoryWorksheet(workbook, report);
        AddTransferWorksheet(workbook, report);
        AddShiftWorksheet(workbook, report);
        AddCustomerWorksheet(workbook, report);
        AddTaxPurchaseWorksheet(workbook, report);
        AddAnalyticsWorksheets(workbook, report);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void AddSummaryWorksheet(XLWorkbook workbook, ReportQueryDto query, ReportsPageDto report)
    {
        var ws = workbook.Worksheets.Add("Summary");
        ws.Cell(1, 1).Value = "SmartPOS Advanced Reports";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 16;

        WriteKeyValue(ws, 3, "From", query.FromUtc?.ToString("yyyy-MM-dd") ?? "Default");
        WriteKeyValue(ws, 4, "To", query.ToUtc?.AddDays(-1).ToString("yyyy-MM-dd") ?? "Default");
        WriteKeyValue(ws, 5, "Generated UTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"));

        var s = report.SalesSummary;
        var rows = new[]
        {
            ("Gross Sales", s.GrossSales),
            ("Returns", s.ReturnsTotal),
            ("Net Sales", s.NetSales),
            ("Tax Collected", s.TaxCollected),
            ("Discounts", s.DiscountTotal),
            ("Gross Profit", s.GrossProfit),
            ("Margin %", s.MarginPercent),
            ("Transactions", (decimal)s.TransactionCount),
            ("Return Count", (decimal)s.ReturnCount),
            ("Void Count", (decimal)s.VoidCount),
            ("Average Basket", s.AverageBasket),
            ("Cash Sales", s.CashSales),
            ("Card Sales", s.CardSales),
            ("Other Sales", s.OtherSales),
            ("Credit Sales", s.CreditSales)
        };

        ws.Cell(7, 1).Value = "Metric";
        ws.Cell(7, 2).Value = "Value";
        StyleHeader(ws.Range(7, 1, 7, 2));

        var row = 8;
        foreach (var item in rows)
        {
            ws.Cell(row, 1).Value = item.Item1;
            ws.Cell(row, 2).Value = item.Item2;
            row++;
        }

        ws.Columns().AdjustToContents();
    }

    private static void AddDailySalesWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Daily Sales"),
            ["Date", "Transactions", "Returns Count", "Voids", "Gross Sales", "Discounts", "Returns", "Net Sales", "Tax", "Gross Profit", "Margin %"],
            report.DailySales,
            r => [r.Date, r.Transactions, r.ReturnsCount, r.VoidCount, r.GrossSales, r.Discounts, r.Returns, r.NetSales, r.Tax, r.GrossProfit, r.MarginPercent]);

    private static void AddStoreSalesWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Sales By Store"),
            ["Store", "Transactions", "Gross Sales", "Returns", "Net Sales", "Discounts", "Gross Profit", "Margin %", "Avg Basket"],
            report.SalesByStore,
            r => [r.StoreName, r.Transactions, r.GrossSales, r.Returns, r.NetSales, r.Discounts, r.GrossProfit, r.MarginPercent, r.AverageBasket]);

    private static void AddCashierSalesWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Sales By Cashier"),
            ["Cashier", "Transactions", "Voids", "Returns", "Gross Sales", "Discounts", "Returns Amount", "Net Sales", "Avg Basket"],
            report.SalesByCashier,
            r => [r.CashierName, r.Transactions, r.VoidCount, r.ReturnCount, r.GrossSales, r.Discounts, r.Returns, r.NetSales, r.AverageBasket]);

    private static void AddProductProfitWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Product Profit"),
            ["SKU", "Product", "Category", "Brand", "Qty Sold", "Qty Returned", "Net Qty", "Revenue", "Cost", "Gross Profit", "Margin %", "Discount"],
            report.ProductPerformance,
            r => [r.Sku, r.ProductName, r.CategoryName, r.BrandName, r.QuantitySold, r.QuantityReturned, r.NetQuantity, r.Revenue, r.Cost, r.GrossProfit, r.MarginPercent, r.DiscountAmount]);

    private static void AddPaymentWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Payments"),
            ["Method", "Payment Lines", "Amount", "Share %"],
            report.PaymentMix,
            r => [r.Method, r.PaymentCount, r.Amount, r.PercentOfTotal]);

    private static void AddDiscountWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Discounts"),
            ["Date", "Sale #", "Store", "Cashier", "Customer", "Invoice Discount", "Line Discount", "Total Discount", "Reason"],
            report.Discounts,
            r => [r.CompletedOn, r.SaleNumber, r.StoreName, r.CashierName, r.CustomerName, r.InvoiceDiscount, r.LineDiscount, r.TotalDiscount, r.Reason ?? string.Empty]);

    private static void AddVoidWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Voids"),
            ["Date", "Sale #", "Store", "Cashier", "Customer", "Amount", "Notes"],
            report.Voids,
            r => [r.CompletedOn, r.SaleNumber, r.StoreName, r.CashierName, r.CustomerName, r.TotalAmount, r.Notes ?? string.Empty]);

    private static void AddReturnWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Returns"),
            ["Date", "Return #", "Store", "Processed By", "Customer", "Method", "Amount", "Tax", "Notes"],
            report.Returns,
            r => [r.ProcessedOn, r.ReturnNumber, r.StoreName, r.ProcessedByName, r.CustomerName, r.RefundMethod, r.TotalAmount, r.TaxAmount, r.Notes ?? string.Empty]);

    private static void AddInventoryWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Inventory"),
            ["Store", "SKU", "Product", "Category", "Brand", "On Hand", "Reorder Level", "Cost", "Stock Value", "Low Stock", "Out Of Stock"],
            report.CurrentStock,
            r => [r.StoreName, r.Sku, r.ProductName, r.CategoryName, r.BrandName, r.QuantityOnHand, r.ReorderLevel, r.CostPrice, r.StockValue, r.IsLowStock, r.IsOutOfStock]);

    private static void AddTransferWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Transfers"),
            ["Transfer #", "Source Store", "Destination Store", "Status", "Requested On", "Qty Requested", "Qty Dispatched", "Qty Received"],
            report.StockTransfers,
            r => [r.TransferNumber, r.SourceStoreName, r.DestinationStoreName, r.Status, r.RequestedOn, r.QuantityRequested, r.QuantityDispatched, r.QuantityReceived]);

    private static void AddShiftWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Shifts"),
            ["Shift #", "Store", "Register", "Opened By", "Opened On", "Closed On", "Status", "Opening Float", "Expected Cash", "Counted Cash", "Variance"],
            report.Shifts,
            r => [r.ShiftNumber, r.StoreName, r.RegisterName, r.OpenedByName, r.OpenedOn, r.ClosedOn, r.Status, r.OpeningFloat, r.ExpectedCash, r.CountedCash, r.Variance]);

    private static void AddCustomerWorksheet(XLWorkbook workbook, ReportsPageDto report) =>
        WriteTable(workbook.Worksheets.Add("Customers"),
            ["Customer", "Phone", "Invoices", "Spend", "Returns", "Balance", "Last Purchase", "Avg Basket"],
            report.Customers,
            r => [r.CustomerName, r.Phone ?? string.Empty, r.InvoiceCount, r.TotalSpend, r.Returns, r.Balance, r.LastPurchaseOn, r.AverageBasket]);

    private static void AddTaxPurchaseWorksheet(XLWorkbook workbook, ReportsPageDto report)
    {
        var ws = workbook.Worksheets.Add("Tax & Purchases");
        ws.Cell(1, 1).Value = "Tax Summary";
        ws.Cell(1, 1).Style.Font.Bold = true;
        WriteKeyValue(ws, 2, "Taxable Sales", report.Tax.TaxableSales);
        WriteKeyValue(ws, 3, "GST Collected", report.Tax.GstCollected);
        WriteKeyValue(ws, 4, "Exempt Sales", report.Tax.ExemptSales);
        WriteKeyValue(ws, 5, "Purchase Count", report.Purchases.PurchaseCount);
        WriteKeyValue(ws, 6, "Total Purchased", report.Purchases.TotalPurchased);
        WriteKeyValue(ws, 7, "Purchase Tax", report.Purchases.TotalTax);

        ws.Cell(9, 1).Value = "Tax By Rate";
        ws.Cell(9, 1).Style.Font.Bold = true;
        WriteTable(ws, 10, ["Rate", "Taxable", "Tax"], report.Tax.ByRate, r => [r.Rate, r.TaxableAmount, r.TaxAmount]);

        var start = 13 + report.Tax.ByRate.Count;
        ws.Cell(start, 1).Value = "Purchases By Supplier";
        ws.Cell(start, 1).Style.Font.Bold = true;
        WriteTable(ws, start + 1, ["Supplier", "GRNs", "Total"], report.Purchases.BySupplier, r => [r.SupplierName, r.PurchaseCount, r.TotalAmount]);
        ws.Columns().AdjustToContents();
    }

    private static void WriteKeyValue(IXLWorksheet ws, int row, string key, object? value)
    {
        ws.Cell(row, 1).Value = key;
        SetCell(ws, row, 2, value);
    }

    private static void WriteTable<T>(IXLWorksheet ws, IReadOnlyList<string> headers, IEnumerable<T> rows, Func<T, object?[]> map) =>
        WriteTable(ws, 1, headers, rows, map);

    private static void WriteTable<T>(IXLWorksheet ws, int startRow, IReadOnlyList<string> headers, IEnumerable<T> rows, Func<T, object?[]> map)
    {
        for (var i = 0; i < headers.Count; i++)
        {
            ws.Cell(startRow, i + 1).Value = headers[i];
        }

        StyleHeader(ws.Range(startRow, 1, startRow, headers.Count));

        var row = startRow + 1;
        foreach (var item in rows)
        {
            var values = map(item);
            for (var col = 0; col < values.Length; col++)
            {
                SetCell(ws, row, col + 1, values[col]);
            }
            row++;
        }

        ws.SheetView.FreezeRows(startRow);
        ws.Columns().AdjustToContents();
    }

    private static void SetCell(IXLWorksheet ws, int row, int column, object? value)
    {
        var cell = ws.Cell(row, column);
        switch (value)
        {
            case null:
                cell.Value = string.Empty;
                break;
            case DateTime dt:
                cell.Value = dt;
                cell.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm";
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                cell.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm";
                break;
            case decimal d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case double d:
                cell.Value = d;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case float f:
                cell.Value = f;
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case bool b:
                cell.Value = b ? "Yes" : "No";
                break;
            default:
                cell.Value = value.ToString() ?? string.Empty;
                break;
        }
    }

    private static void StyleHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#f3f4f6");
        range.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
    }

    private static string FormatStore(Store store) => string.IsNullOrWhiteSpace(store.Code)
        ? store.Name
        : store.Code + " · " + store.Name;

    private static string GetUserName(IReadOnlyDictionary<string, string> usersById, string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return "Unknown user";
        }

        return usersById.TryGetValue(userId, out var name) ? name : userId;
    }

    private static decimal SafePercent(decimal numerator, decimal denominator) =>
        denominator == 0 ? 0 : Math.Round(numerator / denominator * 100m, 1);

    private sealed record SaleLineWithHeader(Sale Sale, SaleLine Line);
    private sealed record ReturnLineWithHeader(SaleReturn Return, SaleReturnLine Line);
}
