using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Infrastructure.Services.Management;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Tests;

public sealed partial class PosRegressionTests
{
    [Fact]
    public async Task Restocking_an_empty_pos_product_records_an_adjustment_and_allows_checkout()
    {
        await using var fixture = await PosFixture.CreateAsync();
        (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand = 0;
        await fixture.Db.SaveChangesAsync();
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        var result = await inventory.PostAdjustmentAsync(new StockAdjustmentDto
        {
            StoreId = fixture.Store.Id, MovementType = "Adjustment", Reference = "POS correction",
            Lines = [new() { ProductId = fixture.Product.Id, QuantityChange = 5 }]
        });
        Assert.True(result.Succeeded, result.Error);
        var ledger = await fixture.Db.StockLedgers.SingleAsync();
        Assert.Equal(StockMovementType.Adjustment, ledger.MovementType);
        Assert.Equal(5m, ledger.QuantityAfter);
        Assert.False(Assert.Single(await fixture.Pos.GetProductsAsync(fixture.Store.Id)).IsOutOfStock);
        Assert.True((await fixture.Pos.CompleteSaleAsync(fixture.Checkout())).Succeeded);
        Assert.Equal(4m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
    }

    [Fact]
    public async Task Variant_adjustment_preserves_base_product_stock_and_other_store_stock()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var branch = new Store { Code = "BR", Name = "Branch" };
        var variant = new ProductVariant { ProductId = fixture.Product.Id, Sku = "TEA-V", Name = "Variant" };
        fixture.Db.AddRange(branch, variant,
            new Stock { ProductId = fixture.Product.Id, ProductVariantId = variant.Id, StoreId = fixture.Store.Id, QuantityOnHand = 0 },
            new Stock { ProductId = fixture.Product.Id, ProductVariantId = variant.Id, StoreId = branch.Id, QuantityOnHand = 20 });
        await fixture.Db.SaveChangesAsync();
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        Assert.True((await inventory.PostAdjustmentAsync(new StockAdjustmentDto
        {
            StoreId = fixture.Store.Id,
            Lines = [new() { ProductId = fixture.Product.Id, ProductVariantId = variant.Id, QuantityChange = 2.5m }]
        })).Succeeded);
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync(row => row.ProductVariantId == null)).QuantityOnHand);
        Assert.Equal(20m, (await fixture.Db.Stocks.SingleAsync(row => row.StoreId == branch.Id)).QuantityOnHand);
        Assert.Equal(2.5m, (await fixture.Db.Stocks.SingleAsync(row => row.StoreId == fixture.Store.Id && row.ProductVariantId == variant.Id)).QuantityOnHand);
        Assert.Equal(variant.Id, (await fixture.Db.StockLedgers.SingleAsync()).ProductVariantId);
    }

    [Fact]
    public async Task Inventory_service_rejects_a_cashier_without_stock_permission()
    {
        await using var fixture = await PosFixture.CreateAsync();
        fixture.Context.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, fixture.CurrentUser.UserId!),
            new Claim(ClaimTypes.Role, DefaultRolePermissions.Cashier)
        }, "test")));
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inventory.PostAdjustmentAsync(new StockAdjustmentDto
        {
            StoreId = fixture.Store.Id, Lines = [new() { ProductId = fixture.Product.Id, QuantityChange = 5 }]
        }));
        Assert.Equal(10m, (await fixture.Db.Stocks.SingleAsync()).QuantityOnHand);
        Assert.Empty(await fixture.Db.StockLedgers.ToListAsync());
    }

    [Fact]
    public async Task Inventory_service_rejects_adjustments_to_an_unassigned_store()
    {
        await using var fixture = await PosFixture.CreateAsync();
        var branch = new Store { Code = "BR", Name = "Branch" };
        fixture.Db.AddRange(branch, new UserStore { UserId = fixture.CurrentUser.UserId!, StoreId = fixture.Store.Id });
        await fixture.Db.SaveChangesAsync();
        fixture.Context.RestrictToManager();
        await using var provider = fixture.ReadServices();
        var inventory = new InventoryService(fixture.Db, provider.GetRequiredService<IServiceScopeFactory>(),
            fixture.Tenant, fixture.Context, fixture.Access, new DbContextGate(fixture.Db));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inventory.PostAdjustmentAsync(new StockAdjustmentDto
        {
            StoreId = branch.Id, Lines = [new() { ProductId = fixture.Product.Id, QuantityChange = 5 }]
        }));
        Assert.Empty(await fixture.Db.StockLedgers.ToListAsync());
    }
}
