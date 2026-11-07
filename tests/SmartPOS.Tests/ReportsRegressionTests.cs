using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Services.Management;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Tests;

public sealed partial class PosRegressionTests
{
    private static ReportService ReportsFor(PosFixture fixture) =>
        new(fixture.Db, new DbContextGate(fixture.Db), fixture.Access);

    private static ReportQueryDto ReportPeriod(PosFixture fixture, Guid? storeId = null) => new()
    {
        StoreId = storeId,
        FromUtc = fixture.Clock.UtcNow.Date,
        ToUtc = fixture.Clock.UtcNow.Date.AddDays(1)
    };

    private static Sale ReportSale(PosFixture fixture, string number, decimal quantity = 1, Guid? variantId = null,
        SaleStatus status = SaleStatus.Completed) => new()
    {
        StoreId = fixture.Store.Id, RegisterId = fixture.Register.Id, CashierUserId = fixture.CurrentUser.UserId!,
        SaleNumber = number, CompletedOn = fixture.Clock.UtcNow, Status = status,
        Subtotal = quantity * 1000, TotalAmount = quantity * 1000,
        Lines = [new SaleLine { ProductId = fixture.Product.Id, ProductVariantId = variantId, Sku = fixture.Product.Sku,
            ProductName = fixture.Product.Name, Quantity = quantity, UnitPrice = 1000, CostPrice = 500,
            LineTotal = quantity * 1000, LineNumber = 1 }]
    };

    [Fact]
    public async Task Report_sales_register_and_weekdays_reconcile_discounted_refunds()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        var line = await fixture.Db.SaleLines.SingleAsync();
        Assert.True((await fixture.Pos.ProcessReturnAsync(fixture.Return(sale.Data!.SaleId, line.Id))).Succeeded);
        var report = await ReportsFor(fixture).GetReportsAsync(ReportPeriod(fixture));
        var invoice = Assert.Single(report.SalesRegister);
        Assert.Equal(900m, invoice.TotalAmount);
        Assert.Equal(100m, invoice.Discounts);
        Assert.Equal(900m, invoice.RefundsInPeriod);
        Assert.Equal(0m, invoice.NetSales);
        Assert.Equal(report.SalesSummary.NetSales, report.WeekdaySales.Sum(row => row.NetSales));
        Assert.Equal(7, report.WeekdaySales.Count);
    }

    [Fact]
    public async Task Weekday_averages_include_calendar_days_with_no_sales()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = ReportSale(fixture, "MONDAY");
        sale.CompletedOn = new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc);
        fixture.Db.Sales.Add(sale);
        await fixture.Db.SaveChangesAsync();
        var report = await ReportsFor(fixture).GetReportsAsync(new ReportQueryDto
            { FromUtc = new DateTime(2026, 10, 19), ToUtc = new DateTime(2026, 11, 2) });
        var monday = report.WeekdaySales.Single(row => row.DayName == "Monday");
        Assert.Equal(2, monday.DaysInPeriod);
        Assert.Equal(500m, monday.AverageDailySales);
        Assert.Equal(14, report.WeekdaySales.Sum(row => row.DaysInPeriod));
    }

    [Fact]
    public async Task Slow_stock_separates_variants_and_ignores_voided_sales()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var variant = new ProductVariant { ProductId = fixture.Product.Id, Sku = "TEA-V", Name = "Small", CostPrice = 25 };
        fixture.Db.AddRange(variant,
            new Stock { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, ProductVariantId = variant.Id, QuantityOnHand = 3 },
            ReportSale(fixture, "BASE", 6), ReportSale(fixture, "VARIANT", 1, variant.Id),
            ReportSale(fixture, "VOIDED-VARIANT", 100, variant.Id, SaleStatus.Voided));
        await fixture.Db.SaveChangesAsync();
        var report = await ReportsFor(fixture).GetReportsAsync(ReportPeriod(fixture));
        var slow = Assert.Single(report.SlowMovingStock);
        Assert.Equal("TEA-V", slow.Sku);
        Assert.Equal(1m, slow.QuantitySold);
        Assert.Equal(75m, slow.StockValue);
        Assert.Equal(3m, slow.StockCoverDays);
        var stock = report.CurrentStock.Single(row => row.ProductVariantId == variant.Id);
        Assert.Equal(25m, stock.CostPrice);
    }

    [Fact]
    public async Task New_report_data_and_export_respect_store_access_and_date_boundaries()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var hidden = new Store { Name = "Hidden", Code = "HIDDEN" };
        var supplier = new Supplier { Name = "Supplier" };
        fixture.Db.AddRange(hidden, supplier,
            new UserStore { UserId = fixture.CurrentUser.UserId!, StoreId = fixture.Store.Id },
            new Purchase { StoreId = hidden.Id, SupplierId = supplier.Id, PurchaseNumber = "SECRET", Status = PurchaseStatus.Received, ReceivedOn = fixture.Clock.UtcNow, TotalAmount = 999 },
            new StockLedger { StoreId = hidden.Id, ProductId = fixture.Product.Id, MovementType = StockMovementType.Opening, Reference = "SECRET", QuantityChange = 999 },
            new StockLedger { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, MovementType = StockMovementType.Opening, Reference = "VISIBLE", QuantityChange = 10, QuantityAfter = 10 });
        var boundary = ReportSale(fixture, "NEXT-DAY");
        boundary.CompletedOn = fixture.Clock.UtcNow.Date.AddDays(1);
        fixture.Db.Sales.Add(boundary);
        await fixture.Db.SaveChangesAsync();
        fixture.Context.RestrictToManager();
        var service = ReportsFor(fixture);
        var report = await service.GetReportsAsync(ReportPeriod(fixture));
        Assert.Empty(report.PurchaseRegister);
        Assert.Empty(report.SalesRegister);
        Assert.Equal("VISIBLE", Assert.Single(report.StockLedger).Reference);
        Assert.All(report.SlowMovingStock, row => Assert.DoesNotContain("HIDDEN", row.StoreName));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetReportsAsync(ReportPeriod(fixture, hidden.Id)));
        var bytes = await service.ExportReportsWorkbookAsync(ReportPeriod(fixture));
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(25, workbook.Worksheets.Count);
        Assert.DoesNotContain(workbook.Worksheets.SelectMany(sheet => sheet.CellsUsed()).Select(cell => cell.GetString()), value => value.Contains("SECRET"));
        Assert.Equal("VISIBLE", workbook.Worksheet("Stock Ledger").Cell(2, 9).GetString());
    }

    [Fact]
    public async Task Purchase_register_contains_only_received_purchases_and_reconciles_to_supplier_summary()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var supplier = new Supplier { Name = "Supplier" };
        fixture.Db.Add(supplier);
        foreach (var status in new[] { PurchaseStatus.Received, PurchaseStatus.Draft, PurchaseStatus.Cancelled })
            fixture.Db.Purchases.Add(new Purchase
            {
                StoreId = fixture.Store.Id, SupplierId = supplier.Id, PurchaseNumber = status.ToString(), Status = status,
                ReceivedOn = fixture.Clock.UtcNow, Subtotal = 100, TaxAmount = 18, TotalAmount = 118,
                Lines = [new PurchaseLine { ProductId = fixture.Product.Id, Sku = "TEA", ProductName = "Tea", Quantity = 2, UnitCost = 50, LineTotal = 118 }]
            });
        await fixture.Db.SaveChangesAsync();
        var report = await ReportsFor(fixture).GetReportsAsync(ReportPeriod(fixture, fixture.Store.Id));
        var received = Assert.Single(report.PurchaseRegister);
        Assert.Equal(2m, received.Quantity);
        Assert.Equal(118m, received.TotalAmount);
        Assert.Equal(received.TotalAmount, report.Purchases.TotalPurchased);
        Assert.Equal(received.Tax, report.Purchases.TotalTax);
        Assert.Equal(received.TotalAmount, Assert.Single(report.Purchases.BySupplier).TotalAmount);
    }

    [Fact]
    public async Task Product_profit_report_includes_more_than_fifty_products_and_does_not_double_count_renames()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var first = ReportSale(fixture, "FIRST");
        var renamed = ReportSale(fixture, "RENAMED");
        renamed.Lines.Single().ProductName = "Renamed tea";
        fixture.Db.AddRange(first, renamed);
        for (var i = 0; i < 51; i++)
        {
            var product = new Product { Sku = $"P{i}", Name = $"Product {i}", SellPrice = 1000 };
            var sale = ReportSale(fixture, $"SALE{i}");
            var line = sale.Lines.Single();
            line.ProductId = product.Id;
            line.Sku = product.Sku;
            line.ProductName = product.Name;
            fixture.Db.AddRange(product, sale);
        }
        await fixture.Db.SaveChangesAsync();
        var service = ReportsFor(fixture);
        var report = await service.GetReportsAsync(ReportPeriod(fixture));
        Assert.Equal(52, report.ProductPerformance.Count);
        Assert.Equal(50, report.TopProducts.Count);
        Assert.Equal(2m, report.ProductPerformance.Single(row => row.ProductId == fixture.Product.Id).QuantitySold);
        Assert.Equal(report.SalesSummary.NetSales, report.ProductPerformance.Sum(row => row.Revenue));
        var bytes = await service.ExportReportsWorkbookAsync(ReportPeriod(fixture));
        using var workbook = new XLWorkbook(new MemoryStream(bytes));
        Assert.Equal(53, workbook.Worksheet("Product Profit").LastRowUsed()!.RowNumber());
    }
}
