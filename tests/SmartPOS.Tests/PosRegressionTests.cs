using System.Security.Claims;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Options;
using SmartPOS.Application.Sales;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Persistence.Interceptors;
using SmartPOS.Infrastructure.Services;
using SmartPOS.Infrastructure.Services.Management;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Tests;

public sealed partial class PosRegressionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(999)]
    [InlineData(1001)]
    public async Task Checkout_rejects_a_price_that_differs_from_the_catalog(decimal price)
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].UnitPrice = price;
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.Equal("PRICE_CHANGED", result.ErrorCode);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(0.0001)]
    public async Task Checkout_rejects_invalid_quantities(decimal quantity)
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].Quantity = quantity;
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.Equal("INVALID_QUANTITY", result.ErrorCode);
        Assert.Empty(await fixture.Db.StockLedgers.ToListAsync());
    }

    [Fact]
    public async Task Checkout_rejects_an_inactive_product()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Product.IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("PRODUCT_UNAVAILABLE", (await fixture.Pos.CompleteSaleAsync(fixture.Checkout())).ErrorCode);
    }

    [Fact]
    public async Task Checkout_rejects_a_product_hidden_from_the_store()
    {
        await using var fixture = await PosFixture.CreateAsync();
        (await fixture.Db.ProductStoreAssignments.SingleAsync()).IsVisibleOnPos = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("PRODUCT_UNAVAILABLE", (await fixture.Pos.CompleteSaleAsync(fixture.Checkout())).ErrorCode);
    }

    [Fact]
    public async Task Checkout_rejects_a_variant_from_another_product()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var other = new Product { Name = "Other", Sku = "OTHER", SellPrice = 1000 };
        var variant = new ProductVariant { ProductId = other.Id, Name = "Other variant", Sku = "OTHER-V" };
        fixture.Db.AddRange(other, variant);
        await fixture.Db.SaveChangesAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].VariantId = variant.Id;
        Assert.Equal("INVALID_VARIANT", (await fixture.Pos.CompleteSaleAsync(checkout)).ErrorCode);
    }

    [Fact]
    public async Task Discounts_work_without_manager_credentials_when_disabled()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(900m, result.Data!.TotalAmount);
        Assert.Empty(await fixture.Db.Set<ManagerApprovalLog>().ToListAsync());
    }

    [Fact]
    public async Task Discount_approval_is_enforced_when_the_setting_is_enabled()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Security.RequireDiscountManagerApproval = true;
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        Assert.Equal("APPROVAL_REQUIRED", (await fixture.Pos.CompleteSaleAsync(checkout)).ErrorCode);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
    }

    [Fact]
    public async Task Checkout_uses_authoritative_product_names()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].Name = "Altered name";
        checkout.Lines[0].Sku = "FAKE";
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(result.Succeeded, result.Error);
        var line = await fixture.Db.SaleLines.SingleAsync();
        Assert.Equal(fixture.Product.Name, line.ProductName);
        Assert.Equal(fixture.Product.Sku, line.Sku);
    }

    [Fact]
    public async Task Checkout_rechecks_customer_credit_limit()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var customer = new Customer { Name = "Customer", CreditLimit = 1000, Balance = 500 };
        fixture.Db.Customers.Add(customer);
        await fixture.Db.SaveChangesAsync();
        var checkout = fixture.Checkout();
        checkout.CustomerId = customer.Id;
        checkout.PaymentMethod = "Credit";
        Assert.Equal("CREDIT_LIMIT", (await fixture.Pos.CompleteSaleAsync(checkout)).ErrorCode);
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
    }

    [Fact]
    public async Task Full_return_refunds_the_discounted_invoice_value()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(sale.Succeeded, sale.Error);
        var line = await fixture.Db.SaleLines.SingleAsync();
        var result = await fixture.Pos.ProcessReturnAsync(fixture.Return(sale.Data!.SaleId, line.Id));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(900m, result.Data!.TotalAmount);
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Duplicate_return_lines_are_rejected_without_refunding_or_restocking()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        var line = await fixture.Db.SaleLines.SingleAsync();
        var request = fixture.Return(sale.Data!.SaleId, line.Id);
        request.Lines.Add(new SaleReturnLineInputDto { SaleLineId = line.Id, QuantityReturned = 1 });
        Assert.Equal("DUPLICATE_RETURN_LINE", (await fixture.Pos.ProcessReturnAsync(request)).ErrorCode);
        Assert.Empty(await fixture.Db.SaleReturns.ToListAsync());
        Assert.Equal(9m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task A_completed_return_cannot_be_submitted_again()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        var line = await fixture.Db.SaleLines.SingleAsync();
        var request = fixture.Return(sale.Data!.SaleId, line.Id);
        Assert.True((await fixture.Pos.ProcessReturnAsync(request)).Succeeded);
        Assert.False((await fixture.Pos.ProcessReturnAsync(request)).Succeeded);
        Assert.Single(await fixture.Db.SaleReturns.ToListAsync());
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Mixed_credit_return_reverses_only_the_credit_component()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var customer = new Customer { Name = "Customer", CreditLimit = 5000 };
        fixture.Db.Customers.Add(customer);
        await fixture.Db.SaveChangesAsync();
        var checkout = fixture.Checkout();
        checkout.CustomerId = customer.Id;
        checkout.Payments = [new() { Method = "Cash", Amount = 400 }, new() { Method = "Credit", Amount = 600 }];
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(sale.Succeeded, sale.Error);
        var line = await fixture.Db.SaleLines.SingleAsync();
        var result = await fixture.Pos.ProcessReturnAsync(fixture.Return(sale.Data!.SaleId, line.Id));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0m, (await fixture.Db.Customers.SingleAsync()).Balance);
        Assert.Equal(-600m, (await fixture.Db.CustomerLedgers.SingleAsync(entry => entry.EntryType == CustomerLedgerEntryType.Refund)).Amount);
    }

    [Fact]
    public async Task Void_requires_a_backend_validated_approval()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        var result = await fixture.Pos.VoidSaleAsync(new VoidSaleDto { SaleId = sale.Data!.SaleId, Reason = "Duplicate sale" });
        Assert.Equal("APPROVAL_REQUIRED", result.ErrorCode);
        Assert.Equal(SaleStatus.Completed, (await fixture.Db.Sales.SingleAsync()).Status);
    }

    [Fact]
    public async Task A_valid_void_approval_is_consumed_with_the_transaction()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        var approval = new ManagerApprovalLog
        {
            StoreId = fixture.Store.Id, ApprovalType = ManagerApprovalType.VoidSale,
            RequestedByUserId = fixture.CurrentUser.UserId!, ApprovedByUserId = fixture.CurrentUser.UserId!,
            ApprovedByUserName = "Manager", Amount = 1000, Reference = sale.Data!.SaleNumber,
            Reason = "Duplicate sale", ApprovedOn = fixture.Clock.UtcNow
        };
        fixture.Db.Add(approval);
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Pos.VoidSaleAsync(new VoidSaleDto
            { SaleId = sale.Data.SaleId, Reason = "Duplicate sale", ManagerApprovalId = approval.Id });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(ManagerApprovalStatus.Consumed, (await fixture.Db.Set<ManagerApprovalLog>().SingleAsync()).Status);
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Receipt_service_blocks_an_unassigned_store()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var sale = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        fixture.Context.RestrictToManager();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Pos.GetReceiptAsync(sale.Data!.SaleId));
    }

    [Fact]
    public async Task Product_edits_preserve_prices_and_visibility_in_other_stores()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var otherStore = new Store { Name = "Other", Code = "OTHER" };
        fixture.Db.Stores.Add(otherStore);
        fixture.Db.ProductPrices.Add(new ProductPrice { ProductId = fixture.Product.Id, StoreId = otherStore.Id, Price = 777 });
        fixture.Db.ProductStoreAssignments.Add(new ProductStoreAssignment
            { ProductId = fixture.Product.Id, StoreId = otherStore.Id, IsVisibleOnPos = true });
        fixture.Db.UserStores.Add(new UserStore { UserId = fixture.CurrentUser.UserId!, StoreId = fixture.Store.Id });
        await fixture.Db.SaveChangesAsync();
        fixture.Context.RestrictToManager();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var product = await service.GetAsync(fixture.Product.Id);
        Assert.DoesNotContain(product!.StorePrices, price => price.StoreId == otherStore.Id);
        product.Name = "Updated";
        Assert.True((await service.UpdateAsync(product)).Succeeded);
        Assert.Equal(777m, (await fixture.Db.ProductPrices.SingleAsync(price => price.StoreId == otherStore.Id)).Price);
        Assert.True((await fixture.Db.ProductStoreAssignments.SingleAsync(item => item.StoreId == otherStore.Id)).IsVisibleOnPos);
    }

    [Fact]
    public async Task Store_access_is_denied_after_a_user_is_deactivated()
    {
        await using var fixture = await PosFixture.CreateAsync();
        (await fixture.Db.Users.SingleAsync()).IsActive = false;
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Access.GetAllowedStoreIdsAsync());
    }

    [Fact]
    public async Task Failed_checkout_cannot_leak_an_incomplete_sale_into_a_later_save()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].Quantity = 11;
        checkout.AmountTendered = 11000;
        Assert.Equal("INSUFFICIENT", (await fixture.Pos.CompleteSaleAsync(checkout)).ErrorCode);
        await fixture.Db.SaveChangesAsync();
        Assert.Empty(await fixture.Db.Sales.ToListAsync());
        Assert.Empty(await fixture.Db.StockLedgers.ToListAsync());
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Store_price_overrides_are_authoritative_at_checkout()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Db.ProductPrices.Add(new ProductPrice { ProductId = fixture.Product.Id, StoreId = fixture.Store.Id, Price = 777 });
        await fixture.Db.SaveChangesAsync();
        Assert.Equal("PRICE_CHANGED", (await fixture.Pos.CompleteSaleAsync(fixture.Checkout())).ErrorCode);
        var checkout = fixture.Checkout();
        checkout.Lines[0].UnitPrice = 777;
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(777m, result.Data!.TotalAmount);
    }

    [Fact]
    public async Task Partial_returns_add_up_to_the_paid_invoice_amount()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.Lines[0].Quantity = 3;
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        checkout.AmountTendered = 3000;
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(sale.Succeeded, sale.Error);
        var line = await fixture.Db.SaleLines.SingleAsync();
        var refunded = 0m;
        for (var index = 0; index < 3; index++)
        {
            var result = await fixture.Pos.ProcessReturnAsync(fixture.Return(sale.Data!.SaleId, line.Id));
            Assert.True(result.Succeeded, result.Error);
            refunded += result.Data!.TotalAmount;
        }
        Assert.Equal(2900m, refunded);
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Cash_only_customers_cannot_buy_on_credit()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var customer = new Customer { Name = "Cash customer", CreditLimit = 0 };
        fixture.Db.Customers.Add(customer);
        await fixture.Db.SaveChangesAsync();
        var checkout = fixture.Checkout();
        checkout.CustomerId = customer.Id;
        checkout.PaymentMethod = "Credit";
        Assert.Equal("CREDIT_DISABLED", (await fixture.Pos.CompleteSaleAsync(checkout)).ErrorCode);
    }

    [Fact]
    public async Task Drawer_balance_includes_movements_with_identical_timestamps()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Register.HasCashDrawer = true;
        var shift = new Shift
        {
            RegisterId = fixture.Register.Id, StoreId = fixture.Store.Id, ShiftNumber = "SHIFT-1",
            Status = ShiftStatus.Open, OpenedByUserId = fixture.CurrentUser.UserId!, OpenedOn = fixture.Clock.UtcNow
        };
        fixture.Db.AddRange(shift,
            new CashMovement { StoreId = fixture.Store.Id, ShiftId = shift.Id, Amount = 100, BalanceAfter = 100,
                MovementType = CashMovementType.OpeningFloat, OccurredOn = fixture.Clock.UtcNow },
            new CashMovement { StoreId = fixture.Store.Id, ShiftId = shift.Id, Amount = 10, BalanceAfter = 110,
                MovementType = CashMovementType.Sale, OccurredOn = fixture.Clock.UtcNow });
        await fixture.Db.SaveChangesAsync();
        var result = await fixture.Pos.CompleteSaleAsync(fixture.Checkout());
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1110m, (await fixture.Pos.GetContextAsync(fixture.Store.Id))!.ShiftCashBalance);
    }

    [Fact]
    public async Task Dashboard_reconciles_invoice_discounts_and_returns()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Promotion";
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        await using var provider = fixture.ReadServices();
        var dashboard = new DashboardService(provider.GetRequiredService<IServiceScopeFactory>(), fixture.Clock,
            new TestStoreContext(fixture.Store.Id), fixture.Tenant, fixture.Context);
        var before = await dashboard.GetSummaryAsync();
        Assert.Equal(900m, before.TodaySales);
        Assert.Equal(400m, before.TodayProfit);
        var line = await fixture.Db.SaleLines.SingleAsync();
        var returned = await fixture.Pos.ProcessReturnAsync(fixture.Return(sale.Data!.SaleId, line.Id));
        Assert.True(returned.Succeeded, returned.Error);
        var after = await dashboard.GetSummaryAsync();
        Assert.Equal(0m, after.TodaySales);
        Assert.Equal(0m, after.TodayProfit);
        Assert.Equal(1, after.TodayTransactions);
    }

    [Fact]
    public async Task Reports_do_not_count_line_discounts_twice()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var checkout = fixture.Checkout();
        checkout.InvoiceDiscountAmount = 100;
        checkout.DiscountReason = "Invoice promotion";
        checkout.Lines[0].DiscountAmount = 50;
        checkout.Lines[0].DiscountReason = "Line promotion";
        Assert.True((await fixture.Pos.CompleteSaleAsync(checkout)).Succeeded);
        var reports = new ReportService(fixture.Db, new DbContextGate(fixture.Db), fixture.Access);
        var result = await reports.GetReportsAsync(new ReportQueryDto
            { StoreId = fixture.Store.Id, FromUtc = fixture.Clock.UtcNow.AddDays(-1), ToUtc = fixture.Clock.UtcNow.AddDays(1) });
        Assert.Equal(150m, result.SalesSummary.DiscountTotal);
        Assert.Equal(350m, result.SalesSummary.GrossProfit);
    }

    [Fact]
    public async Task Failed_product_creation_saves_neither_the_product_nor_its_dependents()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.FailVariantWrites = true;
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var product = new ProductDto
        {
            Sku = "NEW", Name = "New product", SellPrice = 100,
            StorePrices = [new() { StoreId = fixture.Store.Id, Price = 90 }],
            StoreAssignments = [new() { StoreId = fixture.Store.Id, IsVisibleOnPos = true }],
            Variants = [new() { Sku = "NEW-V", Name = "Variant", SellPrice = 100 }]
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync(product));
        fixture.FailVariantWrites = false;
        await fixture.Db.SaveChangesAsync();
        Assert.DoesNotContain(await fixture.Db.Products.ToListAsync(), row => row.Sku == "NEW");
        Assert.Empty(await fixture.Db.ProductVariants.ToListAsync());
        Assert.Empty(await fixture.Db.ProductPrices.ToListAsync());
    }

    [Fact]
    public async Task Product_creation_saves_the_complete_product_graph()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var result = await service.CreateAsync(new ProductDto
        {
            Sku = "NEW", Name = "New product", SellPrice = 100,
            AdditionalBarcodes = ["NEW-123"],
            StorePrices = [new() { StoreId = fixture.Store.Id, Price = 90 }],
            StoreAssignments = [new() { StoreId = fixture.Store.Id, IsVisibleOnPos = true }],
            Variants = [new() { Sku = "NEW-V", Name = "Variant", SellPrice = 110 }]
        });
        Assert.True(result.Succeeded, result.Error);
        Assert.Single(await fixture.Db.ProductPrices.Where(row => row.ProductId == result.Data).ToListAsync());
        Assert.Single(await fixture.Db.ProductBarcodes.Where(row => row.ProductId == result.Data).ToListAsync());
        Assert.Single(await fixture.Db.ProductVariants.Where(row => row.ProductId == result.Data).ToListAsync());
        Assert.Single(await fixture.Db.ProductStoreAssignments.Where(row => row.ProductId == result.Data).ToListAsync());
    }

    [Fact]
    public async Task A_product_sku_can_be_reused_by_a_different_company()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var otherTenant = new Tenant { Name = "Other company" };
        fixture.Db.AddRange(otherTenant, new Product { TenantId = otherTenant.Id, Sku = "NEW", Name = "Other company item" });
        await fixture.Db.SaveChangesAsync();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var result = await service.CreateAsync(new ProductDto { Sku = "NEW", Name = "Our item", SellPrice = 100 });
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(fixture.Tenant.TenantId, (await fixture.Db.Products.SingleAsync(row => row.Id == result.Data)).TenantId);
    }

    [Fact]
    public async Task Opening_stock_cannot_be_reposted_after_stock_is_depleted()
    {
        await using var fixture = await PosFixture.CreateAsync();
        (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand = 0;
        fixture.Db.StockLedgers.Add(new StockLedger
        {
            StoreId = fixture.Store.Id, ProductId = fixture.Product.Id,
            MovementType = StockMovementType.Opening, QuantityChange = 10, QuantityAfter = 10
        });
        await fixture.Db.SaveChangesAsync();
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        var result = await inventory.PostOpeningStockAsync(fixture.Store.Id, fixture.Product.Id, 5);
        Assert.Equal("ALREADY_SET", result.ErrorCode);
        Assert.Equal(0m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Scanned_variants_use_the_same_sku_and_price_as_checkout()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var variant = new ProductVariant
            { ProductId = fixture.Product.Id, Sku = "TEA-LARGE", Name = "Large", Barcode = "12345", SellPrice = 1200 };
        fixture.Db.AddRange(variant, new Stock
            { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, ProductVariantId = variant.Id, QuantityOnHand = 3 });
        await fixture.Db.SaveChangesAsync();
        var scanned = await fixture.Pos.LookupBarcodeAsync(fixture.Store.Id, "12345");
        Assert.Equal("TEA-LARGE", scanned!.Sku);
        Assert.Equal(1200m, scanned.UnitPrice);
        var checkout = fixture.Checkout();
        checkout.AmountTendered = 1200;
        checkout.Lines[0].VariantId = scanned.VariantId;
        checkout.Lines[0].UnitPrice = scanned.UnitPrice;
        checkout.Lines[0].Sku = scanned.Sku;
        var result = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("TEA-LARGE", (await fixture.Db.SaleLines.SingleAsync()).Sku);
    }

    [Fact]
    public async Task Adjustment_dialog_cannot_bypass_the_opening_stock_history_check()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Db.StockLedgers.Add(new StockLedger
            { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, MovementType = StockMovementType.Opening,
                QuantityChange = 10, QuantityAfter = 10 });
        await fixture.Db.SaveChangesAsync();
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        var result = await inventory.PostAdjustmentAsync(new StockAdjustmentDto
        {
            StoreId = fixture.Store.Id, MovementType = "Opening",
            Lines = [new() { ProductId = fixture.Product.Id, QuantityChange = 5 }]
        });
        Assert.Equal("ALREADY_SET", result.ErrorCode);
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task New_products_use_the_default_price_in_all_stores_without_price_overrides()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var otherStore = new Store { Name = "Branch", Code = "BRANCH" };
        fixture.Db.Stores.Add(otherStore);
        await fixture.Db.SaveChangesAsync();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var result = await service.CreateAsync(new ProductDto
        {
            Sku = "AUTO", Name = "Automatic pricing", SellPrice = 125.50m,
            StorePrices = [new() { StoreId = fixture.Store.Id }, new() { StoreId = otherStore.Id }],
            StoreAssignments =
            [
                new() { StoreId = fixture.Store.Id, IsVisibleOnPos = true },
                new() { StoreId = otherStore.Id, IsVisibleOnPos = true }
            ]
        });
        Assert.True(result.Succeeded, result.Error);
        Assert.Empty(await fixture.Db.ProductPrices.Where(row => row.ProductId == result.Data).ToListAsync());
        foreach (var storeId in new[] { fixture.Store.Id, otherStore.Id })
        {
            var catalog = await fixture.Pos.GetProductsAsync(storeId, search: "AUTO");
            Assert.Equal(125.50m, Assert.Single(catalog).UnitPrice);
        }
        var reopened = await service.GetAsync(result.Data);
        Assert.Equal(125.50m, reopened!.SellPrice);
        Assert.Empty(reopened.StorePrices);
    }

    [Theory]
    [InlineData(90)]
    [InlineData(0)]
    public async Task Default_price_changes_update_inheriting_stores_and_preserve_custom_prices(decimal customPrice)
    {
        await using var fixture = await PosFixture.CreateAsync();
        var otherStore = new Store { Name = "Branch", Code = "BRANCH" };
        fixture.Db.AddRange(otherStore,
            new ProductStoreAssignment { ProductId = fixture.Product.Id, StoreId = otherStore.Id, IsVisibleOnPos = true },
            new ProductPrice { ProductId = fixture.Product.Id, StoreId = otherStore.Id, Price = customPrice });
        await fixture.Db.SaveChangesAsync();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var product = (await service.GetAsync(fixture.Product.Id))!;
        product.SellPrice = 1200;
        product.StorePrices.Add(new ProductStorePriceDto { StoreId = fixture.Store.Id });
        Assert.True((await service.UpdateAsync(product)).Succeeded);
        Assert.Equal(1200m, Assert.Single(await fixture.Pos.GetProductsAsync(fixture.Store.Id)).UnitPrice);
        Assert.Equal(customPrice, Assert.Single(await fixture.Pos.GetProductsAsync(otherStore.Id)).UnitPrice);
        var reopened = (await service.GetAsync(fixture.Product.Id))!;
        Assert.Equal(customPrice, Assert.Single(reopened.StorePrices).Price);
        Assert.All(reopened.StoreAssignments, assignment => Assert.True(assignment.IsVisibleOnPos));
    }

    [Fact]
    public async Task Resetting_a_store_price_restores_inheritance_after_save_and_at_checkout()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Db.ProductPrices.Add(new ProductPrice
            { ProductId = fixture.Product.Id, StoreId = fixture.Store.Id, Price = 777 });
        await fixture.Db.SaveChangesAsync();
        var service = new ProductService(fixture.Db, fixture.Access, fixture.Tenant, new DbContextGate(fixture.Db));
        var product = (await service.GetAsync(fixture.Product.Id))!;
        Assert.Single(product.StorePrices).Price = null;
        Assert.True((await service.UpdateAsync(product)).Succeeded);
        var reopened = (await service.GetAsync(fixture.Product.Id))!;
        Assert.Empty(reopened.StorePrices);
        reopened.SellPrice = 1200;
        Assert.True((await service.UpdateAsync(reopened)).Succeeded);
        Assert.Equal(1200m, Assert.Single(await fixture.Pos.GetProductsAsync(fixture.Store.Id)).UnitPrice);
        var checkout = fixture.Checkout();
        checkout.Lines[0].UnitPrice = 1200;
        checkout.AmountTendered = 1200;
        var sale = await fixture.Pos.CompleteSaleAsync(checkout);
        Assert.True(sale.Succeeded, sale.Error);
        Assert.Equal(1200m, sale.Data!.TotalAmount);
    }

    private sealed class PosFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection = new("Data Source=:memory:");
        public TestTenant Tenant { get; } = new();
        public TestContext Context { get; } = new();
        public TestClock Clock { get; } = new();
        public PosSecurityOptions Security { get; } = new();
        public bool FailVariantWrites { get; set; }
        public AppDbContext Db { get; private set; } = null!;
        public CurrentUserService CurrentUser { get; private set; } = null!;
        public StoreAccessService Access { get; private set; } = null!;
        public PosService Pos { get; private set; } = null!;
        public Store Store { get; } = new() { Name = "Main", Code = "MAIN" };
        public Register Register { get; } = new() { Name = "Register", Code = "R1", HasCashDrawer = false };
        public Product Product { get; } = new() { Name = "Tea", Sku = "TEA", SellPrice = 1000m, CostPrice = 500m };

        public static async Task<PosFixture> CreateAsync()
        {
            var fixture = new PosFixture();
            await fixture.connection.OpenAsync();
            fixture.CurrentUser = new CurrentUserService(fixture.Context);
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(fixture.connection)
                .AddInterceptors(new TenantEntityInterceptor(fixture.Tenant),
                    new AuditableEntityInterceptor(fixture.CurrentUser, fixture.Clock), new SoftDeleteInterceptor(),
                    new VariantFailureInterceptor(() => fixture.FailVariantWrites)).Options;
            fixture.Db = new AppDbContext(options, fixture.Tenant);
            await fixture.Db.Database.EnsureCreatedAsync();
            fixture.Register.StoreId = fixture.Store.Id;
            fixture.Db.AddRange(new Tenant { Id = fixture.Tenant.TenantId!.Value, Name = "Company" },
                new ApplicationUser { Id = "operator", UserName = "operator", DisplayName = "Operator", IsActive = true, TenantId = fixture.Tenant.TenantId.Value },
                fixture.Store, fixture.Register, fixture.Product,
                new Stock { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, QuantityOnHand = 10 },
                new ProductStoreAssignment { StoreId = fixture.Store.Id, ProductId = fixture.Product.Id, IsVisibleOnPos = true });
            await fixture.Db.SaveChangesAsync();
            fixture.Access = new StoreAccessService(fixture.Db, fixture.CurrentUser, fixture.Tenant);
            fixture.Pos = new PosService(fixture.Db, fixture.CurrentUser, fixture.Clock, new TestSequence(),
                new DbContextGate(fixture.Db), fixture.Access, fixture.Context, Options.Create(fixture.Security));
            return fixture;
        }

        public ServiceProvider ReadServices()
        {
            var services = new ServiceCollection();
            services.AddScoped<IUserContextAccessor>(_ => new TestContext());
            services.AddScoped<ITenantContext>(_ => new TestTenant());
            services.AddScoped<ICurrentUserService, CurrentUserService>();
            services.AddScoped<AppDbContext>(provider => new AppDbContext(
                new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options,
                provider.GetRequiredService<ITenantContext>()));
            services.AddScoped<IStoreAccessService, StoreAccessService>();
            return services.BuildServiceProvider();
        }

        public PosCheckoutDto Checkout() => new()
        {
            StoreId = Store.Id, RegisterId = Register.Id, AmountTendered = 1000,
            Lines = [new() { ProductId = Product.Id, Sku = Product.Sku, Name = Product.Name, UnitPrice = 1000, Quantity = 1 }]
        };

        public ProcessReturnDto Return(Guid saleId, Guid lineId) => new()
        {
            SaleId = saleId, ReturnReason = "Customer return", RefundMethod = "Cash",
            Lines = [new() { SaleLineId = lineId, QuantityReturned = 1, Disposition = "Restock" }]
        };

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class VariantFailureInterceptor(Func<bool> shouldFail) : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (shouldFail() && eventData.Context!.ChangeTracker.Entries<ProductVariant>().Any())
                throw new InvalidOperationException("Simulated dependent save failure.");
            return ValueTask.FromResult(result);
        }
    }

    private sealed class TestStoreContext(Guid? storeId) : IStoreContext
    {
        public Guid? StoreId { get; private set; } = storeId;
        public IReadOnlyList<Guid> AllowedStoreIds => [];
        public void SetStore(Guid value) => StoreId = value;
        public void ClearStore() => StoreId = null;
    }

    private sealed class TestTenant : ITenantContext
    {
        public Guid? TenantId { get; private set; } = Guid.NewGuid();
        public void SetTenant(Guid tenantId) => TenantId = tenantId;
    }

    private sealed class TestClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);
    }

    private sealed class TestSequence : INumberSequenceService
    {
        private int next;
        public Task<string> GetNextAsync(Guid tenantId, Guid storeId, string sequenceType, string prefix,
            DateTime sequenceDate, int padding = 4, CancellationToken ct = default) => Task.FromResult(prefix + (++next));
    }

    private sealed class TestContext : IUserContextAccessor
    {
        public ClaimsPrincipal? Principal { get; private set; }
        public TestContext() => SetRole(DefaultRolePermissions.BusinessOwner);
        public void RestrictToManager() => SetRole(DefaultRolePermissions.StoreManager);
        private void SetRole(string role) => Principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "operator"), new Claim(ClaimTypes.Name, "Operator"), new Claim(ClaimTypes.Role, role),
            new Claim(Permissions.ClaimType, Permissions.Sales.Operate), new Claim(Permissions.ClaimType, Permissions.Sales.View),
            new Claim(Permissions.ClaimType, Permissions.Sales.Refund), new Claim(Permissions.ClaimType, Permissions.Sales.Void),
            new Claim(Permissions.ClaimType, Permissions.Catalog.InventoryManage)
        }, "test"));
        public void SetPrincipal(ClaimsPrincipal principal) => Principal = principal;
    }
}
