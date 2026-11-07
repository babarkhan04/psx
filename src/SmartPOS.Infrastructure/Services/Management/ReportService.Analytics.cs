using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed partial class ReportService
{
    private static IReadOnlyList<SalesRegisterRowDto> BuildSalesRegister(
        IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns,
        IReadOnlyDictionary<Guid, string> stores, IReadOnlyDictionary<string, string> users)
    {
        var refunds = returns.GroupBy(item => item.OriginalSaleId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.TotalAmount));
        return sales.OrderByDescending(sale => sale.CompletedOn).Select(sale => new SalesRegisterRowDto
        {
            SaleNumber = sale.SaleNumber,
            CompletedOn = sale.CompletedOn,
            StoreName = stores.GetValueOrDefault(sale.StoreId, "Unknown store"),
            CashierName = GetUserName(users, sale.CashierUserId),
            CustomerName = sale.CustomerName,
            Status = sale.Status.ToString(),
            PaymentMethods = string.Join(" / ", sale.Payments.Select(payment => payment.Method.ToString()).Distinct()),
            TotalAmount = sale.TotalAmount,
            Discounts = sale.DiscountAmount,
            Tax = sale.TaxAmount,
            RefundsInPeriod = refunds.GetValueOrDefault(sale.Id),
            NetSales = sale.Status == SaleStatus.Voided ? 0 : sale.TotalAmount - refunds.GetValueOrDefault(sale.Id)
        }).ToList();
    }

    private async Task<IReadOnlyList<PurchaseRegisterRowDto>> BuildPurchaseRegisterAsync(
        Guid? storeId, DateTime fromUtc, DateTime toUtc,
        IReadOnlyDictionary<Guid, string> stores, CancellationToken ct)
    {
        var query = db.Purchases.AsNoTracking().Include(purchase => purchase.Supplier).Include(purchase => purchase.Lines)
            .Where(purchase => reportStoreIds.Contains(purchase.StoreId) && purchase.Status == PurchaseStatus.Received
                && purchase.ReceivedOn >= fromUtc && purchase.ReceivedOn < toUtc);
        if (storeId is { } id) query = query.Where(purchase => purchase.StoreId == id);
        var purchases = await query.ToListAsync(ct);
        return purchases.OrderByDescending(purchase => purchase.ReceivedOn).Select(purchase => new PurchaseRegisterRowDto
        {
            PurchaseNumber = purchase.PurchaseNumber,
            ReceivedOn = purchase.ReceivedOn!.Value,
            StoreName = stores.GetValueOrDefault(purchase.StoreId, "Unknown store"),
            SupplierName = purchase.Supplier?.Name ?? "Unknown supplier",
            LineCount = purchase.Lines.Count,
            Quantity = purchase.Lines.Sum(line => line.Quantity),
            Subtotal = purchase.Subtotal,
            Tax = purchase.TaxAmount,
            TotalAmount = purchase.TotalAmount
        }).ToList();
    }

    private static PurchaseReportSummaryDto BuildPurchaseSummary(IReadOnlyList<PurchaseRegisterRowDto> purchases) => new()
    {
        PurchaseCount = purchases.Count,
        TotalPurchased = purchases.Sum(purchase => purchase.TotalAmount),
        TotalTax = purchases.Sum(purchase => purchase.Tax),
        BySupplier = purchases.GroupBy(purchase => purchase.SupplierName).Select(group => new PurchaseSupplierRowDto
        {
            SupplierName = group.Key,
            PurchaseCount = group.Count(),
            TotalAmount = group.Sum(purchase => purchase.TotalAmount)
        }).OrderByDescending(row => row.TotalAmount).ToList()
    };

    private async Task<IReadOnlyList<StockLedgerReportRowDto>> BuildStockLedgerAsync(
        Guid? storeId, DateTime fromUtc, DateTime toUtc,
        IReadOnlyDictionary<Guid, string> stores, CancellationToken ct)
    {
        var query = db.StockLedgers.AsNoTracking().Include(ledger => ledger.Product).Include(ledger => ledger.ProductVariant)
            .Where(ledger => reportStoreIds.Contains(ledger.StoreId) && ledger.CreatedOn >= fromUtc && ledger.CreatedOn < toUtc);
        if (storeId is { } id) query = query.Where(ledger => ledger.StoreId == id);
        var ledgers = await query.OrderByDescending(ledger => ledger.CreatedOn).ThenBy(ledger => ledger.Id).ToListAsync(ct);
        return ledgers.Select(ledger => new StockLedgerReportRowDto
        {
            OccurredOn = ledger.CreatedOn,
            StoreName = stores.GetValueOrDefault(ledger.StoreId, "Unknown store"),
            Sku = ledger.ProductVariant?.Sku ?? ledger.Product?.Sku ?? string.Empty,
            ProductName = ledger.ProductVariant is null ? ledger.Product?.Name ?? "Unknown product"
                : $"{ledger.Product?.Name} · {ledger.ProductVariant.Name}",
            MovementType = ledger.MovementType.ToString(),
            QuantityIn = Math.Max(0, ledger.QuantityChange),
            QuantityOut = Math.Max(0, -ledger.QuantityChange),
            QuantityAfter = ledger.QuantityAfter,
            Reference = ledger.Reference ?? string.Empty,
            Notes = ledger.Notes ?? string.Empty
        }).ToList();
    }

    private static IReadOnlyList<StockMovementSummaryRowDto> BuildMovementSummary(IReadOnlyList<StockLedgerReportRowDto> ledgers) =>
        ledgers.GroupBy(ledger => ledger.MovementType).Select(group => new StockMovementSummaryRowDto
        {
            MovementType = group.Key,
            MovementCount = group.Count(),
            QuantityIn = group.Sum(ledger => ledger.QuantityIn),
            QuantityOut = group.Sum(ledger => ledger.QuantityOut),
            NetQuantity = group.Sum(ledger => ledger.QuantityIn - ledger.QuantityOut)
        }).OrderByDescending(row => row.MovementCount).ToList();

    private static IReadOnlyList<SlowMovingStockRowDto> BuildSlowMovingStock(
        IReadOnlyList<CurrentStockRowDto> stocks, IReadOnlyList<Sale> sales, DateTime fromUtc, DateTime toUtc)
    {
        var sold = sales.SelectMany(sale => sale.Lines.Select(line => new { sale.StoreId, sale.CompletedOn, Line = line }))
            .GroupBy(item => (item.StoreId, item.Line.ProductId, item.Line.ProductVariantId))
            .ToDictionary(group => group.Key, group => new
            {
                Quantity = group.Sum(item => item.Line.Quantity),
                LastSale = group.Max(item => item.CompletedOn)
            });
        var days = Math.Max(1m, (decimal)(toUtc - fromUtc).TotalDays);
        return stocks.Where(stock => stock.QuantityOnHand > 0).Select(stock =>
        {
            sold.TryGetValue((stock.StoreId, stock.ProductId, stock.ProductVariantId), out var activity);
            var quantity = activity?.Quantity ?? 0;
            return new SlowMovingStockRowDto
            {
                StoreName = stock.StoreName,
                Sku = stock.Sku,
                ProductName = stock.ProductName,
                QuantityOnHand = stock.QuantityOnHand,
                QuantitySold = quantity,
                StockValue = stock.StockValue,
                StockCoverDays = quantity <= 0 ? 0 : Math.Round(stock.QuantityOnHand * days / quantity, 1),
                LastSaleInPeriod = activity?.LastSale
            };
        }).Where(row => row.QuantitySold <= 5).OrderBy(row => row.QuantitySold)
            .ThenByDescending(row => row.StockValue).ThenBy(row => row.Sku).ToList();
    }

    private static IReadOnlyList<WeekdaySalesRowDto> BuildWeekdaySales(
        IReadOnlyList<Sale> sales, IReadOnlyList<SaleReturn> returns, DateTime fromUtc, DateTime toUtc)
    {
        var days = Math.Max(1, (int)Math.Ceiling((toUtc - fromUtc.Date).TotalDays));
        return Enumerable.Range(0, 7).Select(order =>
        {
            var weekday = (DayOfWeek)((order + 1) % 7);
            var offset = ((int)weekday - (int)fromUtc.DayOfWeek + 7) % 7;
            var occurrences = offset >= days ? 0 : (days - 1 - offset) / 7 + 1;
            var daySales = sales.Where(sale => sale.CompletedOn.DayOfWeek == weekday).ToList();
            var net = daySales.Sum(sale => sale.TotalAmount) - returns.Where(item => item.ProcessedOn.DayOfWeek == weekday).Sum(item => item.TotalAmount);
            return new WeekdaySalesRowDto
            {
                DayOrder = order,
                DayName = weekday.ToString(),
                DaysInPeriod = occurrences,
                Transactions = daySales.Count,
                NetSales = net,
                AverageDailySales = occurrences == 0 ? 0 : Math.Round(net / occurrences, 2)
            };
        }).ToList();
    }

    private static void AddAnalyticsWorksheets(XLWorkbook workbook, ReportsPageDto report)
    {
        WriteTable(workbook.Worksheets.Add("Sales Register"),
            ["Completed UTC", "Invoice", "Store", "Cashier", "Customer", "Status", "Payment Methods", "Invoice Total", "Discounts", "Tax", "Refunds In Period", "Net Sales"],
            report.SalesRegister, row => [row.CompletedOn, row.SaleNumber, row.StoreName, row.CashierName, row.CustomerName, row.Status, row.PaymentMethods, row.TotalAmount, row.Discounts, row.Tax, row.RefundsInPeriod, row.NetSales]);
        WriteTable(workbook.Worksheets.Add("Purchase Register"),
            ["Received UTC", "GRN", "Store", "Supplier", "Lines", "Quantity", "Subtotal", "Tax", "Total"],
            report.PurchaseRegister, row => [row.ReceivedOn, row.PurchaseNumber, row.StoreName, row.SupplierName, row.LineCount, row.Quantity, row.Subtotal, row.Tax, row.TotalAmount]);
        WriteTable(workbook.Worksheets.Add("Stock Ledger"),
            ["Occurred UTC", "Store", "SKU", "Product", "Movement", "In", "Out", "Balance After", "Reference", "Notes"],
            report.StockLedger, row => [row.OccurredOn, row.StoreName, row.Sku, row.ProductName, row.MovementType, row.QuantityIn, row.QuantityOut, row.QuantityAfter, row.Reference, row.Notes]);
        WriteTable(workbook.Worksheets.Add("Slow Moving Stock"),
            ["Store", "SKU", "Product", "On Hand Now", "Units Sold In Period", "Current Stock Value", "Cover Days (0 if no sales)", "Last Sale In Period UTC"],
            report.SlowMovingStock, row => [row.StoreName, row.Sku, row.ProductName, row.QuantityOnHand, row.QuantitySold, row.StockValue, row.StockCoverDays, row.LastSaleInPeriod]);
        WriteTable(workbook.Worksheets.Add("Weekday Sales"),
            ["Weekday UTC", "Days In Period", "Transactions", "Net Sales", "Average Per Calendar Day"],
            report.WeekdaySales, row => [row.DayName, row.DaysInPeriod, row.Transactions, row.NetSales, row.AverageDailySales]);
        WriteTable(workbook.Worksheets.Add("Sales By Register"),
            ["Register", "Store", "Transactions", "Net Sales", "Cash", "Card", "Credit"],
            report.SalesByRegister, row => [row.RegisterName, row.StoreName, row.Transactions, row.NetSales, row.CashSales, row.CardSales, row.CreditSales]);
        WriteTable(workbook.Worksheets.Add("Brand Sales"),
            ["Brand", "Quantity", "Revenue", "Profit", "Margin %"],
            report.SalesByBrand, row => [row.BrandName, row.QuantitySold, row.Revenue, row.GrossProfit, row.MarginPercent]);
        WriteTable(workbook.Worksheets.Add("Category Sales"),
            ["Category", "Quantity", "Revenue", "Profit", "Margin %"],
            report.SalesByCategory, row => [row.CategoryName, row.QuantitySold, row.Revenue, row.GrossProfit, row.MarginPercent]);
        WriteTable(workbook.Worksheets.Add("Hourly Sales"),
            ["Hour UTC", "Transactions", "Net Sales", "Average Basket"],
            report.HourlySales, row => [row.HourLabel, row.Transactions, row.NetSales, row.AverageBasket]);
        WriteTable(workbook.Worksheets.Add("Stock Movements"),
            ["Movement", "Entries", "In", "Out", "Net Quantity"],
            report.StockMovements, row => [row.MovementType, row.MovementCount, row.QuantityIn, row.QuantityOut, row.NetQuantity]);
        WriteTable(workbook.Worksheets.Add("Low Stock"),
            ["Store", "SKU", "Product", "On Hand", "Reorder Level", "Stock Value"],
            report.CurrentStock.Where(row => row.IsLowStock || row.IsOutOfStock),
            row => [row.StoreName, row.Sku, row.ProductName, row.QuantityOnHand, row.ReorderLevel, row.StockValue]);
    }
}
