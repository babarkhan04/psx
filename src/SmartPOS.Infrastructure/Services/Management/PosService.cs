using System.Data;
using Microsoft.Extensions.Options;
using SmartPOS.Application.Common.Options;
using SmartPOS.Application.Sales;
using SmartPOS.Domain.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Entities;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class PosService(
    AppDbContext db,
    ICurrentUserService currentUser,
    IDateTimeProvider clock,
    INumberSequenceService numberSequence,
    DbContextGate gate,
    IStoreAccessService storeAccess,
    IUserContextAccessor userContext,
    IOptions<PosSecurityOptions> security) : IPosService
{
    private const int DefaultHeldSaleExpiryMinutes = 24 * 60;
    private const int MinimumHeldSaleExpiryMinutes = 15;
    private const int MaximumHeldSaleExpiryMinutes = 7 * 24 * 60;
    public Task<PosContextDto?> GetContextAsync(Guid storeId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetContextCoreAsync(storeId, ct), ct);

    public Task<IReadOnlyList<PosProductDto>> GetProductsAsync(
        Guid storeId,
        Guid? categoryId = null,
        Guid? brandId = null,
        string? search = null,
        CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetProductsCoreAsync(storeId, categoryId, brandId, search, ct), ct);

    public Task<PosFilterLookupsDto> GetProductFilterLookupsAsync(Guid storeId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetProductFilterLookupsCoreAsync(storeId, ct), ct);

    public Task<PosProductDto?> LookupBarcodeAsync(Guid storeId, string code, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => LookupBarcodeCoreAsync(storeId, code, ct), ct);

    public Task<Result<PosReceiptDto>> CompleteSaleAsync(PosCheckoutDto checkout, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CompleteSaleCoreAsync(checkout, ct), ct);

    public Task<Result<Guid>> HoldSaleAsync(HoldSaleDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => HoldSaleCoreAsync(dto, ct), ct);

    public Task<IReadOnlyList<HeldSaleListItemDto>> GetHeldSalesAsync(Guid storeId, Guid registerId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetHeldSalesCoreAsync(storeId, registerId, ct), ct);

    public Task<HeldSaleDto?> GetHeldSaleAsync(Guid heldSaleId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetHeldSaleCoreAsync(heldSaleId, ct), ct);

    public Task<Result> CancelHeldSaleAsync(Guid heldSaleId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => CancelHeldSaleCoreAsync(heldSaleId, ct), ct);

    public Task<PosReceiptDto?> GetReceiptAsync(Guid saleId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetReceiptCoreAsync(saleId, ct), ct);

    public Task<Result> LogReceiptReprintAsync(ReceiptReprintRequestDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => LogReceiptReprintCoreAsync(dto, ct), ct);

    public Task<Result> ToggleProductFavoriteAsync(Guid productId, bool isFavorite, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ToggleProductFavoriteCoreAsync(productId, isFavorite, ct), ct);

    public Task<SalesPageDto> GetSalesAsync(SalesQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetSalesCoreAsync(query, ct), ct);

    public Task<SaleForReturnDto?> GetSaleForReturnAsync(Guid saleId, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetSaleForReturnCoreAsync(saleId, ct), ct);

    public Task<Result> VoidSaleAsync(VoidSaleDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => VoidSaleCoreAsync(dto, ct), ct);

    public Task<Result<SaleReturnReceiptDto>> ProcessReturnAsync(ProcessReturnDto dto, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => ProcessReturnCoreAsync(dto, ct), ct);

    private async Task<PosContextDto?> GetContextCoreAsync(Guid storeId, CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        var store = await db.Stores.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == storeId && s.IsActive, ct);
        if (store is null)
        {
            return null;
        }

        var register = await db.Registers.AsNoTracking()
            .Where(r => r.StoreId == storeId && r.IsActive)
            .OrderBy(r => r.Code)
            .FirstOrDefaultAsync(ct);

        if (register is null)
        {
            return null;
        }

        var shift = await db.Shifts.AsNoTracking()
            .FirstOrDefaultAsync(s => s.RegisterId == register.Id && s.Status == ShiftStatus.Open, ct);

        decimal? shiftBalance = null;
        if (shift is not null)
        {
            shiftBalance = await CashLedgerHelper.GetBalanceAsync(db, shift.Id, ct);
        }

        return new PosContextDto
        {
            StoreId = store.Id,
            StoreName = $"{store.Code} · {store.Name}",
            RegisterId = register.Id,
            RegisterName = register.Name,
            RequiresShift = register.HasCashDrawer,
            HasOpenShift = shift is not null,
            ShiftId = shift?.Id,
            ShiftNumber = shift?.ShiftNumber,
            ShiftCashBalance = shiftBalance
        };
    }

    private async Task<IReadOnlyList<PosProductDto>> GetProductsCoreAsync(
        Guid storeId,
        Guid? categoryId,
        Guid? brandId,
        string? search,
        CancellationToken ct)
    {
        var query = db.Products.AsNoTracking()
            .Include(p => p.TaxCategory)
            .Include(p => p.Unit)
            .Where(p => p.IsActive
                        && p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos));

        if (categoryId is not null)
        {
            query = query.Where(p => p.CategoryId == categoryId);
        }

        if (brandId is not null)
        {
            query = query.Where(p => p.BrandId == brandId);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                EF.Functions.Like(p.Name, $"%{term}%") ||
                EF.Functions.Like(p.Sku, $"%{term}%") ||
                (p.Barcode != null && EF.Functions.Like(p.Barcode, $"%{term}%")));
        }

        var products = await query
            .OrderByDescending(p => p.IsPosFavorite)
            .ThenBy(p => p.PosFavoriteSortOrder)
            .ThenBy(p => p.Name)
            .Take(120)
            .ToListAsync(ct);
        if (products.Count == 0)
        {
            return [];
        }

        var productIds = products.Select(p => p.Id).ToList();
        var storePrices = await db.ProductPrices.AsNoTracking()
            .Where(pp => pp.StoreId == storeId && productIds.Contains(pp.ProductId))
            .ToDictionaryAsync(pp => pp.ProductId, pp => pp.Price, ct);

        var stocks = await db.Stocks.AsNoTracking()
            .Where(s => s.StoreId == storeId && productIds.Contains(s.ProductId) && s.ProductVariantId == null)
            .ToDictionaryAsync(s => s.ProductId, s => s.QuantityOnHand, ct);

        return products.Select(p => MapProduct(p, storePrices, stocks, variantId: null)).ToList();
    }

    private async Task<PosFilterLookupsDto> GetProductFilterLookupsCoreAsync(Guid storeId, CancellationToken ct)
    {
        var baseQuery = db.Products.AsNoTracking()
            .Where(p => p.IsActive
                        && p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos));

        var categoryRows = await baseQuery
            .Where(p => p.CategoryId != null)
            .Select(p => new { Id = p.CategoryId!.Value, Name = p.Category != null ? p.Category.Name : string.Empty })
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var brandRows = await baseQuery
            .Where(p => p.BrandId != null)
            .Select(p => new { Id = p.BrandId!.Value, Name = p.Brand != null ? p.Brand.Name : string.Empty })
            .Distinct()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return new PosFilterLookupsDto
        {
            Categories = categoryRows
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name })
                .ToList(),
            Brands = brandRows
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new LookupDto { Id = x.Id, Name = x.Name })
                .ToList()
        };
    }

    private async Task<PosProductDto?> LookupBarcodeCoreAsync(Guid storeId, string code, CancellationToken ct)
    {
        var trimmed = code.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        var variant = await db.ProductVariants.AsNoTracking()
            .Include(v => v.Product!).ThenInclude(p => p.TaxCategory)
            .Include(v => v.Product!).ThenInclude(p => p.Unit)
            .FirstOrDefaultAsync(v => v.IsActive
                                      && v.Barcode == trimmed
                                      && v.Product != null
                                      && v.Product.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos), ct);

        if (variant?.Product is { IsActive: true } vp)
        {
            return await MapResolvedProductAsync(storeId, vp, variant.Id, variant.SellPrice, ct);
        }

        var extraBarcode = await db.ProductBarcodes.AsNoTracking()
            .Include(b => b.Product!).ThenInclude(p => p.TaxCategory)
            .Include(b => b.Product!).ThenInclude(p => p.Unit)
            .FirstOrDefaultAsync(b => b.Code == trimmed
                                      && b.Product != null
                                      && b.Product.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos), ct);

        if (extraBarcode?.Product is { IsActive: true } bp)
        {
            return await MapResolvedProductAsync(storeId, bp, null, null, ct);
        }

        var product = await db.Products.AsNoTracking()
            .Include(p => p.TaxCategory)
            .Include(p => p.Unit)
            .FirstOrDefaultAsync(p =>
                p.IsActive
                && p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos)
                && (p.Barcode == trimmed || p.Sku == trimmed), ct);

        if (product is not null)
        {
            return await MapResolvedProductAsync(storeId, product, null, null, ct);
        }

        return null;
    }

    private async Task<PosProductDto> MapResolvedProductAsync(
        Guid storeId,
        Product product,
        Guid? variantId,
        decimal? variantPrice,
        CancellationToken ct)
    {
        var variant = variantId is { } id
            ? await db.ProductVariants.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.ProductId == product.Id, ct)
            : null;
        decimal unitPrice;
        if (variantPrice is not null)
        {
            unitPrice = variantPrice.Value;
        }
        else
        {
            var storePrice = await db.ProductPrices.AsNoTracking()
                .FirstOrDefaultAsync(pp => pp.StoreId == storeId && pp.ProductId == product.Id, ct);
            unitPrice = storePrice?.Price ?? product.SellPrice;
        }

        var stock = await db.Stocks.AsNoTracking()
            .FirstOrDefaultAsync(s => s.StoreId == storeId && s.ProductId == product.Id && s.ProductVariantId == variantId, ct);

        return new PosProductDto
        {
            ProductId = product.Id,
            VariantId = variantId,
            Sku = variant?.Sku ?? product.Sku,
            Name = variant is null ? product.Name : $"{product.Name} · {variant.Name}",
            ImageUrl = product.ImageUrl,
            UnitPrice = unitPrice,
            TaxRate = product.TaxCategory?.Rate ?? 0,
            StockOnHand = stock?.QuantityOnHand ?? 0,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            UnitAbbreviation = product.Unit?.Abbreviation,
            IsFavorite = product.IsPosFavorite,
            FavoriteSortOrder = product.PosFavoriteSortOrder
        };
    }

    private static PosProductDto MapProduct(
        Product product,
        Dictionary<Guid, decimal> storePrices,
        Dictionary<Guid, decimal> stocks,
        Guid? variantId,
        decimal? variantPrice = null)
    {
        var unitPrice = variantPrice
            ?? (storePrices.TryGetValue(product.Id, out var sp) ? sp : product.SellPrice);

        stocks.TryGetValue(product.Id, out var qty);

        return new PosProductDto
        {
            ProductId = product.Id,
            VariantId = variantId,
            Sku = product.Sku,
            Name = product.Name,
            ImageUrl = product.ImageUrl,
            UnitPrice = unitPrice,
            TaxRate = product.TaxCategory?.Rate ?? 0,
            StockOnHand = qty,
            CategoryId = product.CategoryId,
            BrandId = product.BrandId,
            UnitAbbreviation = product.Unit?.Abbreviation,
            IsFavorite = product.IsPosFavorite,
            FavoriteSortOrder = product.PosFavoriteSortOrder
        };
    }

    private async Task<Result<Guid>> HoldSaleCoreAsync(HoldSaleDto dto, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.Operate);
        await storeAccess.EnsureCanAccessStoreAsync(dto.StoreId, ct);
        if (dto.Lines is null || dto.Lines.Count > 500)
            return Result<Guid>.Failure("Invalid cart lines.", "VALIDATION");
        var trusted = await ResolveCheckoutLinesAsync(dto.StoreId, dto.Lines, ct);
        if (!trusted.Succeeded || trusted.Data is null)
            return Result<Guid>.Failure(trusted.Error!, trusted.ErrorCode!);
        dto.Lines = trusted.Data;
        if (!await db.Registers.AnyAsync(r => r.Id == dto.RegisterId && r.StoreId == dto.StoreId && r.IsActive, ct))
            return Result<Guid>.Failure("Register not found or inactive.", "NOT_FOUND");
        if (dto.Lines.Count == 0)
        {
            return Result<Guid>.Failure("Cart is empty.", "EMPTY_CART");
        }

        var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.StoreId, ct);
        if (store is null)
        {
            return Result<Guid>.Failure("Store not found.", "NOT_FOUND");
        }

        var register = await db.Registers.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == dto.RegisterId && r.StoreId == dto.StoreId, ct);
        if (register is null)
        {
            return Result<Guid>.Failure("Register not found.", "NOT_FOUND");
        }

        Shift? shift = null;
        if (register.HasCashDrawer)
        {
            shift = await db.Shifts.AsNoTracking()
                .FirstOrDefaultAsync(s => s.RegisterId == register.Id && s.Status == ShiftStatus.Open, ct);
            if (shift is null)
            {
                return Result<Guid>.Failure("Open a shift before holding a sale.", "NO_SHIFT");
            }
        }

        var discountValidation = ValidateDiscounts(dto.Lines, dto.InvoiceDiscountAmount);
        if (discountValidation is not null)
        {
            return Result<Guid>.Failure(discountValidation, "INVALID_DISCOUNT");
        }

        var totals = CalculateTotals(dto.Lines, dto.InvoiceDiscountAmount);
        var now = clock.UtcNow;
        var expiryMinutes = dto.ExpireAfterMinutes > 0
            ? Math.Clamp(dto.ExpireAfterMinutes, MinimumHeldSaleExpiryMinutes, MaximumHeldSaleExpiryMinutes)
            : DefaultHeldSaleExpiryMinutes;
        var expiresOn = now.AddMinutes(expiryMinutes);
        var heldNumber = await GenerateHeldSaleNumberAsync(store.Code, store.TenantId, dto.StoreId, now, ct);

        var heldSale = new HeldSale
        {
            StoreId = dto.StoreId,
            RegisterId = dto.RegisterId,
            ShiftId = shift?.Id,
            CashierUserId = currentUser.UserId ?? string.Empty,
            HeldNumber = heldNumber,
            Status = HeldSaleStatus.Open,
            CustomerId = dto.CustomerId,
            CustomerName = string.IsNullOrWhiteSpace(dto.CustomerName) ? "Walk-In Customer" : dto.CustomerName.Trim(),
            Subtotal = totals.GrossSubtotal,
            DiscountAmount = totals.TotalDiscount,
            DiscountReason = dto.DiscountReason?.Trim(),
            TaxAmount = totals.TaxAmount,
            TotalAmount = totals.TotalAmount,
            Notes = dto.Notes?.Trim(),
            HeldOn = now,
            ExpiresOn = expiresOn
        };

        db.HeldSales.Add(heldSale);

        var lineNumber = 1;
        foreach (var line in dto.Lines)
        {
            db.HeldSaleLines.Add(new HeldSaleLine
            {
                HeldSaleId = heldSale.Id,
                LineNumber = lineNumber++,
                ProductId = line.ProductId,
                ProductVariantId = line.VariantId,
                Sku = line.Sku,
                ProductName = line.Name,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                DiscountAmount = line.LineDiscount,
                DiscountReason = line.DiscountReason?.Trim(),
                TaxRate = line.TaxRate,
                TaxAmount = line.LineTax,
                LineTotal = line.LineTotal
            });
        }

        await db.SaveChangesAsync(ct);
        return Result<Guid>.Success(heldSale.Id);
    }

    private async Task<IReadOnlyList<HeldSaleListItemDto>> GetHeldSalesCoreAsync(Guid storeId, Guid registerId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        await ExpireOpenHeldSalesAsync(storeId, registerId, now, ct);

        var heldSales = await db.HeldSales.AsNoTracking()
            .Include(h => h.Lines)
            .Where(h => h.StoreId == storeId
                        && h.RegisterId == registerId
                        && h.Status == HeldSaleStatus.Open
                        && (h.ExpiresOn == null || h.ExpiresOn > now))
            .OrderByDescending(h => h.HeldOn)
            .Take(100)
            .ToListAsync(ct);

        if (heldSales.Count == 0)
        {
            return [];
        }

        var cashierIds = heldSales.Select(h => h.CashierUserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var cashiers = await db.Users.AsNoTracking()
            .Where(u => cashierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName ?? "Cashier", ct);

        return heldSales.Select(h => new HeldSaleListItemDto
        {
            Id = h.Id,
            HeldNumber = h.HeldNumber,
            HeldOn = h.HeldOn,
            ExpiresOn = h.ExpiresOn,
            MinutesRemaining = h.ExpiresOn is null ? null : Math.Max(0, (int)Math.Ceiling((h.ExpiresOn.Value - now).TotalMinutes)),
            CashierName = cashiers.GetValueOrDefault(h.CashierUserId, "Cashier"),
            CustomerName = h.CustomerName,
            ItemCount = h.Lines.Count,
            TotalAmount = h.TotalAmount,
            Notes = h.Notes
        }).ToList();
    }

    private async Task<HeldSaleDto?> GetHeldSaleCoreAsync(Guid heldSaleId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var heldSale = await db.HeldSales
            .Include(h => h.Lines)
            .FirstOrDefaultAsync(h => h.Id == heldSaleId, ct);

        if (heldSale is null || heldSale.Status != HeldSaleStatus.Open)
        {
            return null;
        }

        await storeAccess.EnsureCanAccessStoreAsync(heldSale.StoreId, ct);

        if (heldSale.ExpiresOn is not null && heldSale.ExpiresOn <= now)
        {
            heldSale.Status = HeldSaleStatus.Expired;
            heldSale.ExpiredOn = now;
            heldSale.ExpiryReason = "Expired automatically after parked-sale expiry time.";
            await db.SaveChangesAsync(ct);
            return null;
        }

        return new HeldSaleDto
        {
            Id = heldSale.Id,
            HeldNumber = heldSale.HeldNumber,
            StoreId = heldSale.StoreId,
            RegisterId = heldSale.RegisterId,
            CustomerId = heldSale.CustomerId,
            CustomerName = heldSale.CustomerName,
            HeldOn = heldSale.HeldOn,
            ExpiresOn = heldSale.ExpiresOn,
            InvoiceDiscountAmount = Math.Max(0m, heldSale.DiscountAmount - heldSale.Lines.Sum(l => l.DiscountAmount)),
            DiscountReason = heldSale.DiscountReason,
            Notes = heldSale.Notes,
            Lines = heldSale.Lines.OrderBy(l => l.LineNumber).Select(l => new PosCartLineDto
            {
                ProductId = l.ProductId,
                VariantId = l.ProductVariantId,
                Sku = l.Sku,
                Name = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                DiscountReason = l.DiscountReason,
                TaxRate = l.TaxRate
            }).ToList()
        };
    }

    private async Task<Result> CancelHeldSaleCoreAsync(Guid heldSaleId, CancellationToken ct)
    {
        var heldSale = await db.HeldSales.FirstOrDefaultAsync(h => h.Id == heldSaleId && h.Status == HeldSaleStatus.Open, ct);
        if (heldSale is null)
        {
            return Result.Failure("Held sale not found or already closed.", "NOT_FOUND");
        }

        await storeAccess.EnsureCanAccessStoreAsync(heldSale.StoreId, ct);

        var now = clock.UtcNow;
        heldSale.Status = HeldSaleStatus.Cancelled;
        heldSale.CancelledOn = now;
        heldSale.CancelledByUserId = currentUser.UserId;

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<int> ExpireOpenHeldSalesAsync(Guid storeId, Guid registerId, DateTime now, CancellationToken ct)
    {
        var expired = await db.HeldSales
            .Where(h => h.StoreId == storeId
                        && h.RegisterId == registerId
                        && h.Status == HeldSaleStatus.Open
                        && h.ExpiresOn != null
                        && h.ExpiresOn <= now)
            .ToListAsync(ct);

        if (expired.Count == 0)
        {
            return 0;
        }

        foreach (var heldSale in expired)
        {
            heldSale.Status = HeldSaleStatus.Expired;
            heldSale.ExpiredOn = now;
            heldSale.ExpiryReason = "Expired automatically after parked-sale expiry time.";
        }

        await db.SaveChangesAsync(ct);
        return expired.Count;
    }

    private async Task<Result<PosReceiptDto>> CompleteSaleCoreAsync(PosCheckoutDto checkout, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.Operate);
        await storeAccess.EnsureCanAccessStoreAsync(checkout.StoreId, ct);
        if (checkout.Lines is null || checkout.Lines.Count == 0 || checkout.Lines.Count > 500)
            return Result<PosReceiptDto>.Failure("Add between one and 500 cart lines.", "EMPTY_CART");
        checkout.Payments ??= [];
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var now = clock.UtcNow;
            var store = await db.Stores.AsNoTracking().FirstOrDefaultAsync(s => s.Id == checkout.StoreId && s.IsActive, ct);
            if (store is null)
            {
                return Result<PosReceiptDto>.Failure("Store not found.", "NOT_FOUND");
            }

            var register = await db.Registers.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == checkout.RegisterId && r.StoreId == checkout.StoreId && r.IsActive, ct);
            if (register is null)
            {
                return Result<PosReceiptDto>.Failure("Register not found.", "NOT_FOUND");
            }

            var trustedLines = await ResolveCheckoutLinesAsync(checkout.StoreId, checkout.Lines, ct);
            if (!trustedLines.Succeeded || trustedLines.Data is null)
                return Result<PosReceiptDto>.Failure(trustedLines.Error!, trustedLines.ErrorCode!);
            checkout.Lines = trustedLines.Data;

            Shift? shift = null;
            if (register.HasCashDrawer)
            {
                shift = await db.Shifts.FirstOrDefaultAsync(
                    s => s.RegisterId == register.Id && s.Status == ShiftStatus.Open, ct);
                if (shift is null)
                {
                    return Result<PosReceiptDto>.Failure(
                        "Open a shift on this register before processing sales.", "NO_SHIFT");
                }
            }

            var discountValidation = ValidateDiscounts(checkout.Lines, checkout.InvoiceDiscountAmount);
            if (discountValidation is not null)
            {
                return Result<PosReceiptDto>.Failure(discountValidation, "INVALID_DISCOUNT");
            }

            if (checkout.InvoiceDiscountAmount > 0 && ((checkout.DiscountReason?.Trim().Length ?? 0) < 3 || (checkout.DiscountReason?.Trim().Length ?? 0) > 300))
                return Result<PosReceiptDto>.Failure("Enter an invoice discount reason between three and 300 characters.", "INVALID_DISCOUNT");

            var discountApproval = await ValidateDiscountApprovalsAsync(checkout, ct);
            if (!discountApproval.Succeeded)
                return Result<PosReceiptDto>.Failure(discountApproval.Error!, discountApproval.ErrorCode!);

            var totals = CalculateTotals(checkout.Lines, checkout.InvoiceDiscountAmount);
            var total = totals.TotalAmount;

            var paymentResult = ResolvePayments(checkout, total);
            if (!paymentResult.Succeeded || paymentResult.Data is null)
            {
                return Result<PosReceiptDto>.Failure(paymentResult.Error ?? "Invalid payment.", paymentResult.ErrorCode ?? "INVALID_PAYMENT");
            }

            var resolvedPayments = paymentResult.Data.Payments;
            var amountPaid = paymentResult.Data.AmountPaid;
            var changeDue = paymentResult.Data.ChangeDue;
            var creditAmount = resolvedPayments.Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount);
            var cashAmount = resolvedPayments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);

            Customer? selectedCustomer = null;
            if (checkout.CustomerId is { } selectedCustomerId)
            {
                selectedCustomer = await db.Customers.FirstOrDefaultAsync(c => c.Id == selectedCustomerId && c.IsActive, ct);
                if (selectedCustomer is null)
                    return Result<PosReceiptDto>.Failure("Customer not found or inactive.", "NOT_FOUND");
                checkout.CustomerName = selectedCustomer.Name;
            }
            else
            {
                checkout.CustomerName = "Walk-In Customer";
            }

            Customer? creditCustomer = null;
            if (creditAmount > 0)
            {
                if (checkout.CustomerId is not { } customerId)
                {
                    return Result<PosReceiptDto>.Failure("Select a customer when any payment line uses credit/udhaar.", "CUSTOMER_REQUIRED");
                }

                creditCustomer = selectedCustomer;
                if (creditCustomer is null)
                {
                    return Result<PosReceiptDto>.Failure("Customer not found or inactive.", "NOT_FOUND");
                }

                if (creditCustomer.CreditLimit <= 0)
                    return Result<PosReceiptDto>.Failure("This customer is cash-only. Set a credit limit before using udhaar.", "CREDIT_DISABLED");
                if (creditCustomer.Balance + creditAmount > creditCustomer.CreditLimit)
                {
                    var available = Math.Max(0, creditCustomer.CreditLimit - creditCustomer.Balance);
                    return Result<PosReceiptDto>.Failure(
                        $"Credit limit exceeded. Available credit is {available:0.00}.", "CREDIT_LIMIT");
                }
            }


            if (checkout.HeldSaleId is { } preHeldSaleId)
            {
                var preHeldSale = await db.HeldSales.FirstOrDefaultAsync(
                    h => h.Id == preHeldSaleId
                         && h.StoreId == checkout.StoreId
                         && h.RegisterId == checkout.RegisterId, ct);

                if (preHeldSale is null || preHeldSale.Status != HeldSaleStatus.Open)
                {
                    return Result<PosReceiptDto>.Failure("Held sale not found or already closed.", "HELD_SALE_NOT_FOUND");
                }

                if (preHeldSale.ExpiresOn is not null && preHeldSale.ExpiresOn <= now)
                {
                    preHeldSale.Status = HeldSaleStatus.Expired;
                    preHeldSale.ExpiredOn = now;
                    preHeldSale.ExpiryReason = "Expired automatically before checkout.";
                    await db.SaveChangesAsync(ct);
                    return Result<PosReceiptDto>.Failure("This held sale has expired. Please create a new sale.", "HELD_SALE_EXPIRED");
                }
            }

            var saleNumber = await GenerateSaleNumberAsync(store.Code, store.TenantId, checkout.StoreId, now, ct);

            var sale = new Sale
            {
                StoreId = checkout.StoreId,
                RegisterId = checkout.RegisterId,
                ShiftId = shift?.Id,
                CashierUserId = currentUser.UserId ?? string.Empty,
                SaleNumber = saleNumber,
                Status = SaleStatus.Completed,
                CustomerId = checkout.CustomerId,
                CustomerName = string.IsNullOrWhiteSpace(checkout.CustomerName) ? "Walk-In Customer" : checkout.CustomerName.Trim(),
                Subtotal = totals.GrossSubtotal,
                DiscountAmount = totals.TotalDiscount,
                DiscountReason = checkout.DiscountReason?.Trim(),
                DiscountApprovedByUserId = string.IsNullOrWhiteSpace(discountApproval.Data) ? null : discountApproval.Data,
                TaxAmount = totals.TaxAmount,
                TotalAmount = totals.TotalAmount,
                AmountPaid = amountPaid,
                ChangeDue = changeDue,
                Notes = checkout.Notes,
                CompletedOn = now
            };

            db.Sales.Add(sale);

            var lineNumber = 1;
            foreach (var line in checkout.Lines)
            {
                var product = await db.Products.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == line.ProductId, ct);
                if (product is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PosReceiptDto>.Failure($"Product not found: {line.Sku}", "NOT_FOUND");
                }

                var stockResult = await DeductStockAsync(
                    checkout.StoreId,
                    line.ProductId,
                    line.VariantId,
                    line.Quantity,
                    saleNumber,
                    ct);

                if (!stockResult.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PosReceiptDto>.Failure(stockResult.Error!, stockResult.ErrorCode!);
                }

                db.SaleLines.Add(new SaleLine
                {
                    SaleId = sale.Id,
                    LineNumber = lineNumber++,
                    ProductId = line.ProductId,
                    ProductVariantId = line.VariantId,
                    Sku = line.Sku,
                    ProductName = line.Name,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    CostPrice = line.VariantId is { } variantId
                        ? await db.ProductVariants.Where(v => v.Id == variantId && v.ProductId == product.Id)
                            .Select(v => v.CostPrice ?? product.CostPrice).FirstAsync(ct)
                        : product.CostPrice,
                    DiscountAmount = line.LineDiscount,
                    DiscountReason = line.DiscountReason?.Trim(),
                    TaxRate = line.TaxRate,
                    TaxAmount = line.LineTax,
                    LineTotal = line.LineTotal
                });
            }

            foreach (var payment in resolvedPayments)
            {
                db.Payments.Add(new Payment
                {
                    SaleId = sale.Id,
                    Method = payment.Method,
                    Amount = payment.Amount,
                    Reference = string.IsNullOrWhiteSpace(payment.Reference) ? null : payment.Reference.Trim(),
                    ReceivedOn = now
                });
            }

            if (creditAmount > 0 && checkout.CustomerId is { } creditCustomerId)
            {
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == creditCustomerId, ct);
                if (customer is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PosReceiptDto>.Failure("Customer not found.", "NOT_FOUND");
                }

                customer.Balance += creditAmount;
                db.CustomerLedgers.Add(new CustomerLedger
                {
                    CustomerId = customer.Id,
                    EntryType = CustomerLedgerEntryType.Sale,
                    Amount = creditAmount,
                    BalanceAfter = customer.Balance,
                    Reference = saleNumber,
                    EntryDate = now
                });
            }

            if (cashAmount > 0 && shift is not null)
            {
                var cashResult = await CashLedgerHelper.PostMovementAsync(
                    db, shift.Id, checkout.StoreId, CashMovementType.Sale,
                    cashAmount, saleNumber, null, sale.Id, null, now, ct);
                if (!cashResult.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PosReceiptDto>.Failure(cashResult.Error!, cashResult.ErrorCode!);
                }
            }

            if (checkout.HeldSaleId is { } heldSaleId)
            {
                var heldSale = await db.HeldSales.FirstOrDefaultAsync(
                    h => h.Id == heldSaleId
                         && h.StoreId == checkout.StoreId
                         && h.RegisterId == checkout.RegisterId
                         && h.Status == HeldSaleStatus.Open, ct);

                if (heldSale is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result<PosReceiptDto>.Failure("Held sale not found or already closed.", "HELD_SALE_NOT_FOUND");
                }

                heldSale.Status = HeldSaleStatus.Retrieved;
                heldSale.RetrievedOn = now;
                heldSale.RetrievedByUserId = currentUser.UserId;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<PosReceiptDto>.Success(new PosReceiptDto
            {
                SaleId = sale.Id,
                SaleNumber = sale.SaleNumber,
                StoreId = sale.StoreId,
                RegisterId = sale.RegisterId,
                StoreName = $"{store.Code} · {store.Name}",
                RegisterName = register.Name,
                CashierName = currentUser.UserName ?? currentUser.Email ?? "Cashier",
                CustomerName = sale.CustomerName,
                PaymentMethod = FormatPaymentMethods(resolvedPayments),
                Subtotal = totals.GrossSubtotal,
                DiscountAmount = totals.TotalDiscount,
                DiscountReason = checkout.DiscountReason?.Trim(),
                TaxAmount = totals.TaxAmount,
                TotalAmount = totals.TotalAmount,
                AmountPaid = amountPaid,
                ChangeDue = changeDue,
                CompletedOn = now,
                Lines = checkout.Lines.Select(l => new PosReceiptLineDto
                {
                    Sku = l.Sku,
                    Name = l.Name,
                    Quantity = l.Quantity,
                    UnitPrice = l.UnitPrice,
                    DiscountAmount = l.LineDiscount,
                    DiscountReason = l.DiscountReason,
                    LineTotal = l.LineTotal
                }).ToList(),
                Payments = resolvedPayments.Select(p => new PosReceiptPaymentDto
                {
                    Method = p.Method.ToString(),
                    Amount = p.Amount,
                    Reference = p.Reference
                }).ToList()
            });
        });
    }

    private sealed record SaleTotals(
        decimal GrossSubtotal,
        decimal LineDiscount,
        decimal InvoiceDiscount,
        decimal TaxAmount,
        decimal TotalAmount,
        decimal InvoiceDiscountBase)
    {
        public decimal TotalDiscount => LineDiscount + InvoiceDiscount;
    }

    private static string? ValidateDiscounts(IList<PosCartLineDto> lines, decimal invoiceDiscountAmount)
    {
        foreach (var line in lines)
        {
            if (line.Quantity <= 0 || line.Quantity > 999999999m || Math.Round(line.Quantity, 3) != line.Quantity)
                return $"Enter a positive quantity with at most three decimal places for {line.Sku}.";
            if (line.UnitPrice < 0 || line.TaxRate < 0 || line.TaxRate > 100)
                return $"Invalid price or tax for {line.Sku}.";
            if (line.DiscountAmount > 0 && ((line.DiscountReason?.Trim().Length ?? 0) < 3 || (line.DiscountReason?.Trim().Length ?? 0) > 300))
                return $"Enter a reason between three and 300 characters for the discount on {line.Sku}.";
            if (line.DiscountAmount < 0)
            {
                return $"Discount cannot be negative for {line.Sku}.";
            }

            if (Math.Round(line.DiscountAmount, 2) - line.LineGross > 0.005m)
            {
                return $"Discount cannot be greater than line amount for {line.Sku}.";
            }
        }

        var discountBase = Math.Round(lines.Sum(l => l.LineSubtotal) + lines.Sum(l => l.LineTax), 2);
        if (discountBase > 9999999999999999.99m || lines.Sum(line => line.LineGross) > 9999999999999999.99m)
            return "The invoice amount exceeds the supported limit.";
        if (invoiceDiscountAmount < 0)
        {
            return "Invoice discount cannot be negative.";
        }

        if (Math.Round(invoiceDiscountAmount, 2) - discountBase > 0.005m)
        {
            return $"Invoice discount cannot be greater than the remaining invoice total ({discountBase:0.00}).";
        }

        return null;
    }

    private static SaleTotals CalculateTotals(IList<PosCartLineDto> lines, decimal invoiceDiscountAmount)
    {
        var grossSubtotal = Math.Round(lines.Sum(l => l.LineGross), 2);
        var lineDiscount = Math.Round(lines.Sum(l => l.LineDiscount), 2);
        var taxAmount = Math.Round(lines.Sum(l => l.LineTax), 2);
        var invoiceDiscountBase = Math.Round(lines.Sum(l => l.LineSubtotal) + taxAmount, 2);
        var invoiceDiscount = Math.Round(Math.Min(Math.Max(0m, invoiceDiscountAmount), invoiceDiscountBase), 2);
        var totalAmount = Math.Round(invoiceDiscountBase - invoiceDiscount, 2);
        return new SaleTotals(grossSubtotal, lineDiscount, invoiceDiscount, taxAmount, totalAmount, invoiceDiscountBase);
    }

    private sealed record ResolvedPaymentLine(PaymentMethod Method, decimal Amount, string? Reference);

    private sealed record ResolvedPayments(IReadOnlyList<ResolvedPaymentLine> Payments, decimal AmountPaid, decimal ChangeDue);

    private static Result<ResolvedPayments> ResolvePayments(PosCheckoutDto checkout, decimal total)
    {
        var roundedTotal = Math.Round(total, 2);
        if (checkout.AmountTendered < 0 || checkout.AmountTendered > 9999999999999999.99m ||
            checkout.Payments.Count > 100 || checkout.Payments.Any(payment => payment is null || payment.Amount < 0 || payment.Amount > 9999999999999999.99m))
            return Result<ResolvedPayments>.Failure("Enter valid, non-negative payment amounts.", "INVALID_PAYMENT");

        if (checkout.Payments.Count > 0)
        {
            var lines = new List<ResolvedPaymentLine>();
            foreach (var input in checkout.Payments.Where(p => p.Amount > 0))
            {
                if (!Enum.TryParse<PaymentMethod>(input.Method, true, out var method) || !Enum.IsDefined(method))
                {
                    return Result<ResolvedPayments>.Failure($"Invalid payment method: {input.Method}.", "INVALID_PAYMENT");
                }

                lines.Add(new ResolvedPaymentLine(method, Math.Round(input.Amount, 2), input.Reference));
            }

            if (lines.Count == 0)
            {
                return Result<ResolvedPayments>.Failure("Add at least one payment line.", "INVALID_PAYMENT");
            }

            var paidTotal = Math.Round(lines.Sum(p => p.Amount), 2);
            if (Math.Abs(paidTotal - roundedTotal) > 0.005m)
            {
                return Result<ResolvedPayments>.Failure(
                    $"Split payment total must equal invoice total. Paid: {paidTotal:0.00}, Total: {roundedTotal:0.00}.",
                    "PAYMENT_MISMATCH");
            }

            var nonCreditPaid = lines.Where(p => p.Method != PaymentMethod.Credit).Sum(p => p.Amount);
            return Result<ResolvedPayments>.Success(new ResolvedPayments(lines, nonCreditPaid, 0m));
        }

        if (!Enum.TryParse<PaymentMethod>(checkout.PaymentMethod, true, out var paymentMethod) || !Enum.IsDefined(paymentMethod))
        {
            return Result<ResolvedPayments>.Failure("Invalid payment method.", "INVALID_PAYMENT");
        }

        if (paymentMethod == PaymentMethod.Cash)
        {
            if (checkout.AmountTendered < roundedTotal)
            {
                return Result<ResolvedPayments>.Failure("Amount tendered is less than total.", "INSUFFICIENT_TENDER");
            }

            return Result<ResolvedPayments>.Success(new ResolvedPayments(
                [new ResolvedPaymentLine(PaymentMethod.Cash, roundedTotal, null)],
                checkout.AmountTendered,
                checkout.AmountTendered - roundedTotal));
        }

        if (paymentMethod == PaymentMethod.Credit)
        {
            return Result<ResolvedPayments>.Success(new ResolvedPayments(
                [new ResolvedPaymentLine(PaymentMethod.Credit, roundedTotal, null)],
                0m,
                0m));
        }

        return Result<ResolvedPayments>.Success(new ResolvedPayments(
            [new ResolvedPaymentLine(paymentMethod, roundedTotal, null)],
            roundedTotal,
            0m));
    }

    private static string FormatPaymentMethods(IEnumerable<ResolvedPaymentLine> payments)
    {
        var list = payments.ToList();
        if (list.Count == 0)
        {
            return "—";
        }

        return list.Count == 1
            ? list[0].Method.ToString()
            : string.Join(" + ", list.Select(p => p.Method.ToString()).Distinct());
    }

    private static string FormatPaymentMethods(IEnumerable<Payment> payments)
    {
        var list = payments.ToList();
        if (list.Count == 0)
        {
            return "—";
        }

        return list.Count == 1
            ? list[0].Method.ToString()
            : string.Join(" + ", list.Select(p => p.Method.ToString()).Distinct());
    }

    private async Task<Result> DeductStockAsync(
        Guid storeId,
        Guid productId,
        Guid? variantId,
        decimal quantity,
        string reference,
        CancellationToken ct)
    {
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

        if (quantity <= 0)
            return Result.Failure("Sale quantity must be greater than zero.", "INVALID_QUANTITY");
        var change = -quantity;
        var newQty = stock.QuantityOnHand + change;
        if (newQty < 0)
        {
            return Result.Failure($"Insufficient stock for {reference}.", "INSUFFICIENT");
        }

        stock.QuantityOnHand = newQty;
        db.StockLedgers.Add(new StockLedger
        {
            StoreId = storeId,
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = StockMovementType.Sale,
            QuantityChange = change,
            QuantityAfter = newQty,
            Reference = reference
        });

        return Result.Success();
    }

    private Task<string> GenerateHeldSaleNumberAsync(string storeCode, Guid tenantId, Guid storeId, DateTime now, CancellationToken ct)
    {
        var prefix = $"{NormalizeCode(storeCode)}-HELD-{now:yyyyMMdd}-";
        return numberSequence.GetNextAsync(tenantId, storeId, "HELD", prefix, now, 4, ct);
    }

    private Task<string> GenerateSaleNumberAsync(string storeCode, Guid tenantId, Guid storeId, DateTime now, CancellationToken ct)
    {
        var prefix = $"{NormalizeCode(storeCode)}-SALE-{now:yyyyMMdd}-";
        return numberSequence.GetNextAsync(tenantId, storeId, "SALE", prefix, now, 4, ct);
    }

    private async Task<PosReceiptDto?> GetReceiptCoreAsync(Guid saleId, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.View, Permissions.Sales.Operate);
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.ReceiptReprintLogs)
            .Include(s => s.Register)
            .Include(s => s.Register!.Store)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct);

        if (sale is null)
        {
            return null;
        }

        await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);

        var orderedPayments = sale.Payments.OrderBy(p => p.ReceivedOn).ThenBy(p => p.Id).ToList();
        var reprintLogs = sale.ReceiptReprintLogs
            .OrderByDescending(r => r.ReprintedOn)
            .Take(5)
            .Select(r => new ReceiptReprintLogDto
            {
                Id = r.Id,
                ReprintedByName = r.ReprintedByName,
                Reason = r.Reason,
                ReprintedOn = r.ReprintedOn
            })
            .ToList();

        return new PosReceiptDto
        {
            SaleId = sale.Id,
            SaleNumber = sale.SaleNumber,
            StoreId = sale.StoreId,
            RegisterId = sale.RegisterId,
            StoreName = sale.Register?.Store is { } st ? $"{st.Code} · {st.Name}" : string.Empty,
            RegisterName = sale.Register?.Name ?? string.Empty,
            CustomerName = sale.CustomerName,
            PaymentMethod = FormatPaymentMethods(orderedPayments),
            Subtotal = sale.Subtotal,
            DiscountAmount = sale.DiscountAmount,
            DiscountReason = sale.DiscountReason,
            TaxAmount = sale.TaxAmount,
            TotalAmount = sale.TotalAmount,
            AmountPaid = sale.AmountPaid,
            ChangeDue = sale.ChangeDue,
            CompletedOn = sale.CompletedOn,
            Lines = sale.Lines.OrderBy(l => l.LineNumber).Select(l => new PosReceiptLineDto
            {
                Sku = l.Sku,
                Name = l.ProductName,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountAmount = l.DiscountAmount,
                DiscountReason = l.DiscountReason,
                LineTotal = l.LineTotal
            }).ToList(),
            Payments = orderedPayments.Select(p => new PosReceiptPaymentDto
            {
                Method = p.Method.ToString(),
                Amount = p.Amount,
                Reference = p.Reference
            }).ToList(),
            ReprintCount = sale.ReceiptReprintLogs.Count,
            ReprintLogs = reprintLogs
        };
    }

    private async Task<Result> LogReceiptReprintCoreAsync(ReceiptReprintRequestDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Reason) || dto.Reason.Trim().Length < 3)
        {
            return Result.Failure("Enter a reason for reprinting the receipt.", "VALIDATION");
        }

        var sale = await db.Sales.AsNoTracking().FirstOrDefaultAsync(s => s.Id == dto.SaleId, ct);
        if (sale is null)
        {
            return Result.Failure("Sale not found.", "NOT_FOUND");
        }

        await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);

        var userId = currentUser.UserId ?? string.Empty;
        var userName = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.DisplayName ?? u.UserName ?? "User")
            .FirstOrDefaultAsync(ct) ?? "User";

        db.ReceiptReprintLogs.Add(new ReceiptReprintLog
        {
            TenantId = sale.TenantId,
            StoreId = sale.StoreId,
            SaleId = sale.Id,
            SaleNumber = sale.SaleNumber,
            ReprintedByUserId = userId,
            ReprintedByName = userName,
            Reason = dto.Reason.Trim(),
            ReprintedOn = clock.UtcNow
        });

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<Result> ToggleProductFavoriteCoreAsync(Guid productId, bool isFavorite, CancellationToken ct)
    {
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId, ct);
        if (product is null)
        {
            return Result.Failure("Product not found.", "NOT_FOUND");
        }

        product.IsPosFavorite = isFavorite;
        if (isFavorite && product.PosFavoriteSortOrder <= 0)
        {
            var nextOrder = await db.Products
                .Where(p => p.IsPosFavorite)
                .Select(p => (int?)p.PosFavoriteSortOrder)
                .MaxAsync(ct) ?? 0;
            product.PosFavoriteSortOrder = nextOrder + 10;
        }
        else if (!isFavorite)
        {
            product.PosFavoriteSortOrder = 0;
        }

        await db.SaveChangesAsync(ct);
        return Result.Success();
    }

    private async Task<SalesPageDto> GetSalesCoreAsync(SalesQueryDto query, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.View);
        var allowedStoreIds = await storeAccess.GetAllowedStoreIdsAsync(ct);
        if (query.StoreId is { } requestedStore)
            await storeAccess.EnsureCanAccessStoreAsync(requestedStore, ct);
        var q = db.Sales.AsNoTracking()
            .Where(s => allowedStoreIds.Contains(s.StoreId))
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .Include(s => s.Register!).ThenInclude(r => r.Store)
            .AsQueryable();

        if (query.StoreId is { } storeId)
        {
            q = q.Where(s => s.StoreId == storeId);
        }

        if (query.FromUtc is { } from)
        {
            q = q.Where(s => s.CompletedOn >= from);
        }

        if (query.ToUtc is { } to)
        {
            q = q.Where(s => s.CompletedOn < to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var like = $"%{query.Search.Trim()}%";
            q = q.Where(s =>
                EF.Functions.Like(s.SaleNumber, like) ||
                EF.Functions.Like(s.CustomerName, like));
        }

        var sales = await q
            .OrderByDescending(s => s.CompletedOn)
            .Take(query.Take <= 0 ? 100 : query.Take)
            .ToListAsync(ct);

        // Resolve cashier display names in one round-trip.
        var cashierIds = sales.Select(s => s.CashierUserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
        var cashiers = await db.Users.AsNoTracking()
            .Where(u => cashierIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.UserName })
            .ToDictionaryAsync(u => u.Id, u => u.DisplayName ?? u.UserName ?? "Cashier", ct);

        var items = sales.Select(s => new SaleListItemDto
        {
            Id = s.Id,
            SaleNumber = s.SaleNumber,
            CompletedOn = s.CompletedOn,
            StoreName = s.Register?.Store is { } st ? $"{st.Code} · {st.Name}" : string.Empty,
            RegisterName = s.Register?.Name ?? string.Empty,
            CashierName = cashiers.GetValueOrDefault(s.CashierUserId, "Cashier"),
            CustomerName = s.CustomerName,
            PaymentMethod = FormatPaymentMethods(s.Payments),
            Status = s.Status.ToString(),
            ItemCount = s.Lines.Count,
            TotalAmount = s.TotalAmount
        }).ToList();

        var activeSales = sales.Where(s => s.Status != SaleStatus.Voided).ToList();
        var summary = new SalesSummaryDto
        {
            SaleCount = activeSales.Count,
            GrossSales = activeSales.Sum(s => s.TotalAmount),
            TaxCollected = activeSales.Sum(s => s.TaxAmount),
            ItemsSold = activeSales.SelectMany(s => s.Lines).Sum(l => (int)Math.Round(l.Quantity))
        };

        return new SalesPageDto { Sales = items, Summary = summary };
    }

    private async Task<Result> VoidSaleCoreAsync(VoidSaleDto dto, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.Void);
        if (dto.SaleId == Guid.Empty)
        {
            return Result.Failure("Sale is required.", "VALIDATION");
        }

        var reason = dto.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 500)
        {
            return Result.Failure("Void reason is required.", "VALIDATION");
        }

        var now = clock.UtcNow;
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var sale = await db.Sales
                .Include(s => s.Lines)
                .Include(s => s.Payments)
                .Include(s => s.Register)
                .FirstOrDefaultAsync(s => s.Id == dto.SaleId, ct);

            if (sale is null)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure("Sale not found.", "NOT_FOUND");
            }

            if (sale.Status == SaleStatus.Voided)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure("Sale is already voided.", "ALREADY_VOIDED");
            }

            if (sale.Status is SaleStatus.PartiallyReturned or SaleStatus.FullyReturned)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure("Returned sales cannot be voided. Use the return/refund flow instead.", "RETURNED_SALE");
            }

            var hasReturns = await db.SaleReturns.AsNoTracking()
                .AnyAsync(r => r.OriginalSaleId == sale.Id && r.Status == SaleReturnStatus.Completed, ct);
            if (hasReturns)
            {
                await tx.RollbackAsync(ct);
                return Result.Failure("This sale already has return records and cannot be voided.", "RETURNED_SALE");
            }

            await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);
            var approval = await ConsumeApprovalAsync(dto.ManagerApprovalId, ManagerApprovalType.VoidSale,
                sale.StoreId, sale.TotalAmount, sale.SaleNumber, ct);
            if (!approval.Succeeded)
                return Result.Failure(approval.Error!, approval.ErrorCode!);

            var cashAmount = sale.Payments.Where(p => p.Method == PaymentMethod.Cash).Sum(p => p.Amount);
            Shift? openShift = null;
            if (cashAmount > 0 && sale.Register?.HasCashDrawer == true)
            {
                openShift = await db.Shifts.FirstOrDefaultAsync(
                    s => s.RegisterId == sale.RegisterId && s.Status == ShiftStatus.Open, ct);

                if (openShift is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result.Failure(
                        "Open a shift on this register before voiding a cash sale.", "NO_SHIFT");
                }
            }

            var reference = $"VOID {sale.SaleNumber}";
            foreach (var line in sale.Lines)
            {
                var stockResult = await RestoreStockForVoidAsync(
                    sale.StoreId,
                    line.ProductId,
                    line.ProductVariantId,
                    line.Quantity,
                    reference,
                    reason,
                    ct);

                if (!stockResult.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return stockResult;
                }
            }

            if (cashAmount > 0 && openShift is not null)
            {
                var cashResult = await CashLedgerHelper.PostMovementAsync(
                    db,
                    openShift.Id,
                    sale.StoreId,
                    CashMovementType.Refund,
                    -cashAmount,
                    reference,
                    $"Void sale: {reason}",
                    sale.Id,
                    null,
                    now,
                    ct);

                if (!cashResult.Succeeded)
                {
                    await tx.RollbackAsync(ct);
                    return cashResult;
                }
            }

            var creditAmount = sale.Payments.Where(p => p.Method == PaymentMethod.Credit).Sum(p => p.Amount);
            if (creditAmount > 0 && sale.CustomerId is { } customerId)
            {
                var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
                if (customer is null)
                {
                    await tx.RollbackAsync(ct);
                    return Result.Failure("Customer not found for credit reversal.", "NOT_FOUND");
                }

                if (customer.Balance < creditAmount)
                    return Result.Failure("This credit sale has repayments. Use the return/refund flow to settle the paid amount.", "CREDIT_SETTLED");
                customer.Balance -= creditAmount;

                db.CustomerLedgers.Add(new CustomerLedger
                {
                    CustomerId = customer.Id,
                    EntryType = CustomerLedgerEntryType.Refund,
                    Amount = -creditAmount,
                    BalanceAfter = customer.Balance,
                    Reference = reference,
                    Notes = $"Void sale: {reason}",
                    EntryDate = now
                });
            }

            sale.Status = SaleStatus.Voided;
            sale.Notes = AppendAuditNote(sale.Notes, $"Voided on {now:u} by {currentUser.UserName ?? currentUser.Email ?? currentUser.UserId ?? "user"}. Reason: {reason}");

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        });
    }

    private async Task<Result> RestoreStockForVoidAsync(
        Guid storeId,
        Guid productId,
        Guid? variantId,
        decimal quantity,
        string reference,
        string reason,
        CancellationToken ct)
    {
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

        stock.QuantityOnHand += quantity;
        db.StockLedgers.Add(new StockLedger
        {
            StoreId = storeId,
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = StockMovementType.Void,
            QuantityChange = quantity,
            QuantityAfter = stock.QuantityOnHand,
            Reference = reference,
            Notes = reason
        });

        return Result.Success();
    }

    private static string AppendAuditNote(string? existing, string note)
    {
        if (string.IsNullOrWhiteSpace(existing))
        {
            return note;
        }

        return existing.Trim() + Environment.NewLine + note;
    }

    private async Task<SaleForReturnDto?> GetSaleForReturnCoreAsync(Guid saleId, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.Refund);
        var sale = await db.Sales.AsNoTracking()
            .Include(s => s.Lines)
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == saleId, ct);

        if (sale is null || sale.Status == SaleStatus.Voided)
        {
            return null;
        }

        await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);

        var previousLines = await db.SaleReturnLines.AsNoTracking()
            .Where(line => line.SaleReturn != null && line.SaleReturn.OriginalSaleId == saleId &&
                line.SaleReturn.Status == SaleReturnStatus.Completed).ToListAsync(ct);
        var returnedQty = previousLines.GroupBy(line => line.OriginalSaleLineId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.QuantityReturned));

        var refundableLines = SaleFinancials.GetRefundableLines(sale);

        var originalPaymentMethods = FormatPaymentMethods(sale.Payments);

        return new SaleForReturnDto
        {
            SaleId = sale.Id,
            SaleNumber = sale.SaleNumber,
            CustomerName = sale.CustomerName,
            CustomerId = sale.CustomerId,
            OriginalPaymentMethod = originalPaymentMethods == "—" ? "Cash" : originalPaymentMethods,
            TotalAmount = sale.TotalAmount,
            CompletedOn = sale.CompletedOn,
            Lines = sale.Lines.OrderBy(l => l.LineNumber).Select(l =>
            {
                var already = returnedQty.GetValueOrDefault(l.Id, 0m);
                return new SaleReturnLineOptionDto
                {
                    SaleLineId = l.Id,
                    RefundableAmount = refundableLines[l.Id].Total,
                    RefundableTax = refundableLines[l.Id].Tax,
                    RefundedAmount = previousLines.Where(line => line.OriginalSaleLineId == l.Id).Sum(line => line.LineTotal),
                    RefundedTax = previousLines.Where(line => line.OriginalSaleLineId == l.Id).Sum(line => line.TaxAmount),
                    Sku = l.Sku,
                    ProductName = l.ProductName,
                    QuantitySold = l.Quantity,
                    QuantityAlreadyReturned = already,
                    UnitPrice = l.UnitPrice,
                    DiscountedUnitPrice = refundableLines[l.Id].UnitPrice,
                    TaxRate = l.TaxRate,
                    LineTotal = l.LineTotal
                };
            }).Where(l => l.QuantityReturnable > 0).ToList()
        };
    }

    private static decimal GetDiscountedUnitPrice(SaleLine line)
    {
        if (line.Quantity <= 0)
        {
            return line.UnitPrice;
        }

        var unitDiscount = Math.Round(line.DiscountAmount / line.Quantity, 4);
        return Math.Max(0m, line.UnitPrice - unitDiscount);
    }

    private async Task<Result<SaleReturnReceiptDto>> ProcessReturnCoreAsync(ProcessReturnDto dto, CancellationToken ct)
    {
        EnsurePermission(Permissions.Sales.Refund);
        if (dto.Lines is null || dto.Lines.Count == 0 || dto.Lines.Count > 500)
            return Result<SaleReturnReceiptDto>.Failure("Select between one and 500 lines to return.", "EMPTY");
        if (dto.Lines.Any(line => line is null) || SaleFinancials.HasDuplicateReturnLines(dto.Lines.Select(line => line.SaleLineId)))
            return Result<SaleReturnReceiptDto>.Failure("Each sale line can appear only once in a return.", "DUPLICATE_RETURN_LINE");
        if (!Enum.TryParse<SaleReturnType>(dto.ReturnType, true, out var returnType) || !Enum.IsDefined(returnType) ||
            !Enum.TryParse<PaymentMethod>(dto.RefundMethod, true, out var refundMethod) || !Enum.IsDefined(refundMethod))
            return Result<SaleReturnReceiptDto>.Failure("Invalid return type or refund method.", "VALIDATION");
        var reason = dto.ReturnReason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 3 || reason.Length > 300)
            return Result<SaleReturnReceiptDto>.Failure("Enter a return reason between three and 300 characters.", "VALIDATION");
        if (returnType == SaleReturnType.Exchange && refundMethod == PaymentMethod.Cash)
            return Result<SaleReturnReceiptDto>.Failure("Use Card, Other, or Credit for an exchange.", "VALIDATION");

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var sale = await db.Sales.Include(s => s.Lines).Include(s => s.Payments).Include(s => s.Register)
                .FirstOrDefaultAsync(s => s.Id == dto.SaleId, ct);
            if (sale is null || sale.Status is SaleStatus.Voided or SaleStatus.FullyReturned)
                return Result<SaleReturnReceiptDto>.Failure("Sale not found or cannot be returned.", "NOT_FOUND");
            await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);

            var previousReturns = await db.SaleReturns.AsNoTracking().Include(r => r.Lines)
                .Where(r => r.OriginalSaleId == sale.Id && r.Status == SaleReturnStatus.Completed).ToListAsync(ct);
            var previousLines = previousReturns.SelectMany(r => r.Lines).ToList();
            var allocations = SaleFinancials.GetRefundableLines(sale);
            var originals = sale.Lines.ToDictionary(line => line.Id);
            var lines = new List<(SaleLine Original, decimal Quantity, string Disposition, string? Reason, RefundAmounts Amounts)>();
            foreach (var input in dto.Lines)
            {
                if (!originals.TryGetValue(input.SaleLineId, out var original))
                    return Result<SaleReturnReceiptDto>.Failure("Invalid sale line.", "VALIDATION");
                var returned = previousLines.Where(line => line.OriginalSaleLineId == original.Id).ToList();
                var already = returned.Sum(line => line.QuantityReturned);
                if (input.QuantityReturned <= 0 || Math.Round(input.QuantityReturned, 3) != input.QuantityReturned ||
                    input.QuantityReturned > original.Quantity - already)
                    return Result<SaleReturnReceiptDto>.Failure($"Invalid return quantity for {original.Sku}. Returnable: {original.Quantity - already:0.###}.", "VALIDATION");
                var disposition = NormalizeReturnDisposition(input.Disposition);
                if (disposition is null)
                    return Result<SaleReturnReceiptDto>.Failure("Use Restock, Damaged, or Discard for the return disposition.", "VALIDATION");
                var amounts = SaleFinancials.CalculateRefund(allocations[original.Id], already,
                    returned.Sum(line => line.LineTotal), returned.Sum(line => line.TaxAmount), input.QuantityReturned);
                lines.Add((original, input.QuantityReturned, disposition, input.LineReason?.Trim(), amounts));
            }

            var total = lines.Sum(line => line.Amounts.Total);
            if (total > Math.Max(0m, sale.TotalAmount - previousReturns.Sum(r => r.TotalAmount)))
                return Result<SaleReturnReceiptDto>.Failure("The refund exceeds the remaining paid value of this sale.", "REFUND_LIMIT");

            var originalCredit = sale.Payments.Where(payment => payment.Method == PaymentMethod.Credit).Sum(payment => payment.Amount);
            decimal creditReduction = 0;
            Customer? customer = null;
            if (originalCredit > 0 && sale.CustomerId is { } customerId)
            {
                customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
                if (customer is null)
                    return Result<SaleReturnReceiptDto>.Failure("Customer not found for credit settlement.", "NOT_FOUND");
                var references = previousReturns.Select(r => r.ReturnNumber).ToList();
                var creditEntries = await db.CustomerLedgers.AsNoTracking()
                    .Where(entry => entry.CustomerId == customerId && entry.EntryType == CustomerLedgerEntryType.Refund &&
                        entry.Reference != null && references.Contains(entry.Reference)).ToListAsync(ct);
                var reversedCredit = -creditEntries.Sum(entry => entry.Amount);
                creditReduction = Math.Min(total, Math.Min(Math.Max(0m, customer.Balance), Math.Max(0m, originalCredit - reversedCredit)));
            }
            var settlementAmount = total - creditReduction;
            if (refundMethod == PaymentMethod.Credit && settlementAmount > 0)
                return Result<SaleReturnReceiptDto>.Failure("Only unpaid customer credit can be reversed as Credit. Choose Cash, Card, or Other for the paid amount.", "INVALID_CREDIT_REFUND");

            Shift? shift = null;
            if (refundMethod == PaymentMethod.Cash && settlementAmount > 0 && sale.Register?.HasCashDrawer == true)
            {
                shift = await db.Shifts.FirstOrDefaultAsync(s => s.RegisterId == sale.RegisterId && s.Status == ShiftStatus.Open, ct);
                if (shift is null)
                    return Result<SaleReturnReceiptDto>.Failure("Open a shift before issuing a cash refund.", "NO_SHIFT");
            }

            var now = clock.UtcNow;
            var store = await db.Stores.AsNoTracking().FirstAsync(s => s.Id == sale.StoreId, ct);
            var number = await GenerateReturnNumberAsync(store.Code, store.TenantId, sale.StoreId, now, ct);
            var entity = new SaleReturn
            {
                StoreId = sale.StoreId,
                ReturnNumber = number,
                OriginalSaleId = sale.Id,
                RegisterId = sale.RegisterId,
                ShiftId = shift?.Id,
                ProcessedByUserId = currentUser.UserId ?? string.Empty,
                ReturnType = returnType,
                Status = SaleReturnStatus.Completed,
                CustomerId = sale.CustomerId,
                CustomerName = sale.CustomerName,
                Subtotal = lines.Sum(line => line.Amounts.Subtotal),
                TaxAmount = lines.Sum(line => line.Amounts.Tax),
                TotalAmount = total,
                RefundMethod = settlementAmount == 0 && creditReduction > 0 ? PaymentMethod.Credit : refundMethod,
                ReturnReason = reason,
                RefundReference = dto.RefundReference?.Trim(),
                ExchangeReference = dto.ExchangeReference?.Trim(),
                Notes = dto.Notes?.Trim(),
                ProcessedOn = now
            };
            db.SaleReturns.Add(entity);
            var index = 1;
            foreach (var line in lines)
            {
                if (line.Disposition == "Restock")
                {
                    var restock = await RestockAsync(sale.StoreId, line.Original.ProductId,
                        line.Original.ProductVariantId, line.Quantity, number, ct);
                    if (!restock.Succeeded)
                        return Result<SaleReturnReceiptDto>.Failure(restock.Error!, restock.ErrorCode!);
                }
                db.SaleReturnLines.Add(new SaleReturnLine
                {
                    SaleReturnId = entity.Id,
                    OriginalSaleLineId = line.Original.Id,
                    LineNumber = index++,
                    ProductId = line.Original.ProductId,
                    ProductVariantId = line.Original.ProductVariantId,
                    Sku = line.Original.Sku,
                    ProductName = line.Original.ProductName,
                    QuantityReturned = line.Quantity,
                    UnitPrice = allocations[line.Original.Id].UnitPrice,
                    TaxRate = line.Original.TaxRate,
                    TaxAmount = line.Amounts.Tax,
                    LineTotal = line.Amounts.Total,
                    Restock = line.Disposition == "Restock",
                    Disposition = line.Disposition,
                    LineReason = line.Reason
                });
            }
            if (creditReduction > 0 && customer is not null)
            {
                customer.Balance -= creditReduction;
                db.CustomerLedgers.Add(new CustomerLedger
                {
                    CustomerId = customer.Id,
                    EntryType = CustomerLedgerEntryType.Refund,
                    Amount = -creditReduction,
                    BalanceAfter = customer.Balance,
                    Reference = number,
                    Notes = BuildReturnLedgerNote(reason, dto.Notes),
                    EntryDate = now
                });
            }
            if (refundMethod == PaymentMethod.Cash && settlementAmount > 0 && shift is not null)
            {
                var movement = await CashLedgerHelper.PostMovementAsync(db, shift.Id, sale.StoreId,
                    CashMovementType.Refund, -settlementAmount, number, BuildReturnLedgerNote(reason, dto.Notes), null, entity.Id, now, ct);
                if (!movement.Succeeded)
                    return Result<SaleReturnReceiptDto>.Failure(movement.Error!, movement.ErrorCode!);
            }
            sale.Status = sale.Lines.All(original =>
                previousLines.Where(line => line.OriginalSaleLineId == original.Id).Sum(line => line.QuantityReturned) +
                lines.Where(line => line.Original.Id == original.Id).Sum(line => line.Quantity) >= original.Quantity)
                ? SaleStatus.FullyReturned : SaleStatus.PartiallyReturned;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result<SaleReturnReceiptDto>.Success(new SaleReturnReceiptDto
            {
                ReturnId = entity.Id,
                ReturnNumber = number,
                OriginalSaleNumber = sale.SaleNumber,
                CreditReversedAmount = creditReduction,
                SettlementAmount = settlementAmount,
                ReturnReason = reason,
                RefundReference = entity.RefundReference,
                ExchangeReference = entity.ExchangeReference,
                ReturnType = returnType.ToString(),
                RefundMethod = entity.RefundMethod.ToString(),
                CustomerName = sale.CustomerName,
                Subtotal = entity.Subtotal,
                TaxAmount = entity.TaxAmount,
                TotalAmount = total,
                ProcessedOn = now,
                Lines = lines.Select(line => new SaleReturnReceiptLineDto
                {
                    Sku = line.Original.Sku,
                    ProductName = line.Original.ProductName,
                    QuantityReturned = line.Quantity,
                    LineTotal = line.Amounts.Total,
                    Disposition = line.Disposition,
                    LineReason = line.Reason
                }).ToList()
            });
        });
    }

    private static string? NormalizeReturnDisposition(string? disposition)
    {
        if (string.IsNullOrWhiteSpace(disposition))
        {
            return "Restock";
        }

        return disposition.Trim().ToLowerInvariant() switch
        {
            "restock" => "Restock",
            "damaged" => "Damaged",
            "discard" => "Discard",
            _ => null
        };
    }

    private static string BuildReturnLedgerNote(string returnReason, string? notes)
    {
        var trimmedNotes = notes?.Trim();
        return string.IsNullOrWhiteSpace(trimmedNotes)
            ? $"Return reason: {returnReason}"
            : $"Return reason: {returnReason}. {trimmedNotes}";
    }

    private async Task<Result> RestockAsync(
        Guid storeId,
        Guid productId,
        Guid? variantId,
        decimal quantity,
        string reference,
        CancellationToken ct)
    {
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

        stock.QuantityOnHand += quantity;
        db.StockLedgers.Add(new StockLedger
        {
            StoreId = storeId,
            ProductId = productId,
            ProductVariantId = variantId,
            MovementType = StockMovementType.Return,
            QuantityChange = quantity,
            QuantityAfter = stock.QuantityOnHand,
            Reference = reference
        });

        return Result.Success();
    }

    private Task<string> GenerateReturnNumberAsync(string storeCode, Guid tenantId, Guid storeId, DateTime now, CancellationToken ct)
    {
        var prefix = $"{NormalizeCode(storeCode)}-RET-{now:yyyyMMdd}-";
        return numberSequence.GetNextAsync(tenantId, storeId, "RETURN", prefix, now, 4, ct);
    }

    private static string NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? "NA" : code.Trim().ToUpperInvariant().Replace(" ", "");
    private void EnsurePermission(params string[] permissions)
    {
        if (!currentUser.IsAuthenticated || !permissions.Any(permission =>
                userContext.Principal?.HasClaim(Permissions.ClaimType, permission) == true))
            throw new UnauthorizedAccessException("You do not have permission to perform this action.");
    }

    private async Task<Result<IList<PosCartLineDto>>> ResolveCheckoutLinesAsync(
        Guid storeId, IList<PosCartLineDto> inputs, CancellationToken ct)
    {
        if (inputs.Any(line => line is null || line.ProductId == Guid.Empty || line.Quantity <= 0 ||
                line.Quantity > 999999999m || Math.Round(line.Quantity, 3) != line.Quantity))
            return Result<IList<PosCartLineDto>>.Failure("Cart quantities must be positive and use at most three decimal places.", "INVALID_QUANTITY");

        var ids = inputs.Select(line => line.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Include(p => p.TaxCategory)
            .Where(p => ids.Contains(p.Id) && p.IsActive &&
                p.StoreAssignments.Any(a => a.StoreId == storeId && a.IsVisibleOnPos))
            .ToDictionaryAsync(p => p.Id, ct);
        var variantIds = inputs.Where(line => line.VariantId != null).Select(line => line.VariantId!.Value).Distinct().ToList();
        var variants = await db.ProductVariants.AsNoTracking()
            .Where(v => variantIds.Contains(v.Id) && v.IsActive).ToDictionaryAsync(v => v.Id, ct);
        var prices = await db.ProductPrices.AsNoTracking()
            .Where(price => price.StoreId == storeId && ids.Contains(price.ProductId))
            .ToDictionaryAsync(price => price.ProductId, price => price.Price, ct);
        var result = new List<PosCartLineDto>();
        foreach (var input in inputs)
        {
            if (!products.TryGetValue(input.ProductId, out var product))
                return Result<IList<PosCartLineDto>>.Failure("A cart product is inactive or is not assigned to this store. Refresh the cart.", "PRODUCT_UNAVAILABLE");

            ProductVariant? variant = null;
            if (input.VariantId is { } variantId &&
                (!variants.TryGetValue(variantId, out variant) || variant.ProductId != product.Id))
                return Result<IList<PosCartLineDto>>.Failure("A cart variant is invalid or inactive. Refresh the cart.", "INVALID_VARIANT");

            var price = Math.Round(variant?.SellPrice ?? prices.GetValueOrDefault(product.Id, product.SellPrice), 2);
            var taxRate = product.TaxCategory?.Rate ?? 0m;
            if (price < 0 || taxRate < 0 || taxRate > 100)
                return Result<IList<PosCartLineDto>>.Failure("Correct the product price or tax configuration before selling this item.", "INVALID_CATALOG");
            if (Math.Round(input.UnitPrice, 2) != price || input.UnitPrice < 0 || input.TaxRate != taxRate)
                return Result<IList<PosCartLineDto>>.Failure($"The price or tax for {product.Sku} changed. Remove the item and add it again.", "PRICE_CHANGED");

            result.Add(new PosCartLineDto
            {
                ProductId = product.Id,
                VariantId = variant?.Id,
                Sku = variant?.Sku ?? product.Sku,
                Name = variant is null ? product.Name : $"{product.Name} · {variant.Name}",
                Quantity = input.Quantity,
                UnitPrice = price,
                TaxRate = taxRate,
                DiscountAmount = input.DiscountAmount,
                DiscountReason = input.DiscountReason?.Trim(),
                ManagerApprovalId = input.ManagerApprovalId
            });
        }
        return Result<IList<PosCartLineDto>>.Success(result);
    }

    private async Task<Result<string>> ValidateDiscountApprovalsAsync(PosCheckoutDto checkout, CancellationToken ct)
    {
        if (!security.Value.RequireDiscountManagerApproval)
            return Result<string>.Success(string.Empty);

        var approvedBy = string.Empty;
        foreach (var line in checkout.Lines.Where(line => line.DiscountAmount > 0))
        {
            var approval = await ConsumeApprovalAsync(line.ManagerApprovalId, ManagerApprovalType.Discount,
                checkout.StoreId, line.LineDiscount, line.Sku, ct);
            if (!approval.Succeeded)
                return approval;
            approvedBy = approval.Data ?? approvedBy;
        }
        if (checkout.InvoiceDiscountAmount > 0)
        {
            var approval = await ConsumeApprovalAsync(checkout.ManagerApprovalId, ManagerApprovalType.Discount,
                checkout.StoreId, Math.Round(checkout.InvoiceDiscountAmount, 2), "Invoice discount", ct);
            if (!approval.Succeeded)
                return approval;
            approvedBy = approval.Data ?? approvedBy;
        }
        return Result<string>.Success(approvedBy);
    }

    private async Task<Result<string>> ConsumeApprovalAsync(Guid? approvalId, ManagerApprovalType type,
        Guid storeId, decimal amount, string reference, CancellationToken ct)
    {
        if (approvalId is null || approvalId == Guid.Empty)
            return Result<string>.Failure("Manager approval is required for this action.", "APPROVAL_REQUIRED");
        var approval = await db.Set<ManagerApprovalLog>().FirstOrDefaultAsync(log => log.Id == approvalId, ct);
        var now = clock.UtcNow;
        var expiry = Math.Clamp(security.Value.ApprovalExpiryMinutes, 1, 60);
        if (approval is null || approval.Status != ManagerApprovalStatus.Approved || approval.ApprovalType != type ||
            approval.RequestedByUserId != currentUser.UserId || approval.StoreId != storeId ||
            approval.ApprovedOn > now || approval.ApprovedOn < now.AddMinutes(-expiry) ||
            approval.Amount != Math.Round(amount, 2) || !string.Equals(approval.Reference, reference, StringComparison.Ordinal))
            return Result<string>.Failure("Approval is invalid, expired, already used, or belongs to a different action. Request approval again.", "INVALID_APPROVAL");

        if (!await db.Users.AnyAsync(user => user.Id == approval.ApprovedByUserId && user.TenantId == approval.TenantId && user.IsActive, ct))
            return Result<string>.Failure("The approving manager is no longer active.", "INVALID_APPROVAL");
        approval.Status = ManagerApprovalStatus.Consumed;
        approval.Notes = AppendAuditNote(approval.Notes, $"Used on {now:u} by {currentUser.UserId}.");
        return Result<string>.Success(approval.ApprovedByUserId);
    }
}
