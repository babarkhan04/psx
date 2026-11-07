using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Authorization;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Web.ReactApi;

public static class ReactCatalogInventoryApiEndpoints
{
    public static RouteGroupBuilder MapReactCatalogInventoryApi(this RouteGroupBuilder api)
    {
        var catalog = api.MapGroup("/catalog")
            .RequireAuthorization(Permissions.Catalog.ProductsView);

        catalog.MapGet("/bootstrap", GetCatalogBootstrapAsync);
        catalog.MapGet("/products/{productId:guid}", GetProductAsync);

        catalog.MapPost("/products", CreateProductAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        catalog.MapPost("/products/{productId:guid}", UpdateProductAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        catalog.MapPost("/products/{productId:guid}/active", SetProductActiveAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        catalog.MapDelete("/products/{productId:guid}", DeleteProductAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        catalog.MapPost("/assign", AssignProductsAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        catalog.MapPost("/import", ImportProductsAsync)
            .RequireAuthorization(Permissions.Catalog.ProductsManage)
            .DisableAntiforgery();

        var inventory = api.MapGroup("/inventory")
            .RequireAuthorization(Permissions.Catalog.InventoryView);

        inventory.MapGet("/bootstrap", GetInventoryBootstrapAsync);
        inventory.MapGet("/ledger", GetInventoryLedgerAsync);

        inventory.MapPost("/adjustment", PostInventoryAdjustmentAsync)
            .RequireAuthorization(Permissions.Catalog.InventoryManage)
            .DisableAntiforgery();

        inventory.MapPost("/opening", PostOpeningStockAsync)
            .RequireAuthorization(Permissions.Catalog.InventoryManage)
            .DisableAntiforgery();

        return api;
    }

    private static async Task<IResult> GetCatalogBootstrapAsync(
        Guid? storeId,
        string? search,
        bool assignedOnly,
        IProductService productService,
        ICategoryService categoryService,
        IBrandService brandService,
        IUnitService unitService,
        ITaxCategoryService taxCategoryService,
        IStoreService storeService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (storeId is { } selectedStore)
        {
            await storeAccess.EnsureCanAccessStoreAsync(selectedStore, ct);
        }

        var allowedIds = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();
        var stores = (await storeService.GetAllAsync(includeInactive: false, ct))
            .Where(s => allowedIds.Contains(s.Id))
            .Select(s => new ReactStoreOptionDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name
            })
            .ToList();

        var products = await productService.GetAllAsync(search, storeId, assignedOnly, ct);

        return Results.Ok(new ReactCatalogBootstrapDto
        {
            Products = products,
            Categories = await categoryService.GetLookupAsync(ct),
            Brands = await brandService.GetLookupAsync(ct),
            Units = await unitService.GetLookupAsync(ct),
            Taxes = await taxCategoryService.GetLookupAsync(ct),
            Stores = stores
        });
    }

    private static async Task<IResult> GetProductAsync(
        Guid productId,
        IProductService productService,
        IStoreService storeService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var product = await productService.GetAsync(productId, ct);
        if (product is null)
        {
            return Results.NotFound(new { message = "Product not found." });
        }

        var allowedIds = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();
        var stores = (await storeService.GetAllAsync(includeInactive: false, ct))
            .Where(s => allowedIds.Contains(s.Id))
            .ToList();

        var prices = product.StorePrices.ToDictionary(x => x.StoreId, x => x.Price);
        product.StorePrices = stores.Select(s => new ProductStorePriceDto
        {
            StoreId = s.Id,
            StoreName = $"{s.Code} · {s.Name}",
            Price = prices.TryGetValue(s.Id, out var price) ? price : null
        }).ToList();

        var assignments = product.StoreAssignments.ToDictionary(x => x.StoreId, x => x.IsVisibleOnPos);
        product.StoreAssignments = stores.Select(s => new ProductStoreAssignmentDto
        {
            StoreId = s.Id,
            StoreName = $"{s.Code} · {s.Name}",
            IsVisibleOnPos = assignments.TryGetValue(s.Id, out var visible) && visible
        }).ToList();

        return Results.Ok(product);
    }

    private static async Task<IResult> CreateProductAsync(
        ProductDto dto,
        IProductService productService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var allowedIds = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();
        if (dto.StorePrices.Any(x => !allowedIds.Contains(x.StoreId)) ||
            dto.StoreAssignments.Any(x => !allowedIds.Contains(x.StoreId)))
        {
            return Results.Forbid();
        }

        var result = await productService.CreateAsync(dto, ct);
        return FromResult(result);
    }

    private static async Task<IResult> UpdateProductAsync(
        Guid productId,
        ProductDto dto,
        IProductService productService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (dto.Id != Guid.Empty && dto.Id != productId)
        {
            return Results.BadRequest(new { message = "Product id does not match the route." });
        }

        var existing = await productService.GetAsync(productId, ct);
        if (existing is null)
        {
            return Results.NotFound(new { message = "Product not found." });
        }

        var allowedIds = (await storeAccess.GetAllowedStoreIdsAsync(ct)).ToHashSet();

        if (dto.StorePrices.Any(x => !allowedIds.Contains(x.StoreId)) ||
            dto.StoreAssignments.Any(x => !allowedIds.Contains(x.StoreId)))
        {
            return Results.Forbid();
        }

        dto.Id = productId;
        dto.StorePrices = existing.StorePrices
            .Where(x => !allowedIds.Contains(x.StoreId))
            .Concat(dto.StorePrices.Where(x => allowedIds.Contains(x.StoreId)))
            .GroupBy(x => x.StoreId)
            .Select(g => g.Last())
            .ToList();

        dto.StoreAssignments = existing.StoreAssignments
            .Where(x => !allowedIds.Contains(x.StoreId))
            .Concat(dto.StoreAssignments.Where(x => allowedIds.Contains(x.StoreId)))
            .GroupBy(x => x.StoreId)
            .Select(g => g.Last())
            .ToList();

        var result = await productService.UpdateAsync(dto, ct);
        return FromResult(result);
    }

    private static async Task<IResult> SetProductActiveAsync(
        Guid productId,
        ReactSetProductActiveRequest request,
        IProductService productService,
        CancellationToken ct)
    {
        var result = await productService.SetActiveAsync(productId, request.IsActive, ct);
        return FromResult(result);
    }

    private static async Task<IResult> DeleteProductAsync(
        Guid productId,
        IProductService productService,
        CancellationToken ct)
    {
        var result = await productService.DeleteAsync(productId, ct);
        return FromResult(result);
    }

    private static async Task<IResult> AssignProductsAsync(
        ReactProductAssignmentRequest request,
        IProductService productService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(request.StoreId, ct);
        var result = await productService.AssignProductsToStoreAsync(
            request.ProductIds.Distinct().ToList(),
            request.StoreId,
            request.IsVisible,
            ct);

        return FromResult(result);
    }

    private static async Task<IResult> ImportProductsAsync(
        Guid? storeId,
        HttpRequest request,
        IProductImportExportService importService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (storeId is { } selectedStore)
        {
            await storeAccess.EnsureCanAccessStoreAsync(selectedStore, ct);
        }

        var form = await request.ReadFormAsync(ct);
        var file = form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No import file was selected." });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return Results.BadRequest(new { message = "Import file must be 10 MB or smaller." });
        }

        await using var stream = file.OpenReadStream();
        var result = await importService.ImportAsync(stream, storeId, ct);

        return result.Succeeded
            ? Results.Ok(result.Data)
            : Results.BadRequest(new
            {
                message = result.Error ?? "Product import failed.",
                errorCode = result.ErrorCode,
                data = result.Data
            });
    }

    private static async Task<IResult> GetInventoryBootstrapAsync(
        Guid storeId,
        bool lowStockOnly,
        int? ledgerTake,
        IInventoryService inventoryService,
        IProductService productService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);

        var stock = await inventoryService.GetStockAsync(storeId, lowStockOnly, ct);
        var ledger = await inventoryService.GetLedgerAsync(storeId, null, Math.Clamp(ledgerTake ?? 150, 1, 500), ct);
        var products = await productService.GetAllAsync(null, storeId, assignedOnly: false, ct);

        return Results.Ok(new ReactInventoryBootstrapDto
        {
            Stock = stock,
            Ledger = ledger,
            Products = products
        });
    }

    private static async Task<IResult> GetInventoryLedgerAsync(
        Guid storeId,
        Guid? productId,
        int? take,
        IInventoryService inventoryService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        var rows = await inventoryService.GetLedgerAsync(
            storeId,
            productId,
            Math.Clamp(take ?? 150, 1, 500),
            ct);

        return Results.Ok(rows);
    }

    private static async Task<IResult> PostInventoryAdjustmentAsync(
        StockAdjustmentDto dto,
        IInventoryService inventoryService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(dto.StoreId, ct);
        var result = await inventoryService.PostAdjustmentAsync(dto, ct);
        return FromResult(result);
    }

    private static async Task<IResult> PostOpeningStockAsync(
        ReactOpeningStockRequest request,
        IInventoryService inventoryService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(request.StoreId, ct);
        var result = await inventoryService.PostOpeningStockAsync(
            request.StoreId,
            request.ProductId,
            request.Quantity,
            request.VariantId,
            ct);

        return FromResult(result);
    }

    private static IResult FromResult(Result result) =>
        result.Succeeded
            ? Results.NoContent()
            : Results.BadRequest(new
            {
                message = result.Error ?? "The operation could not be completed.",
                errorCode = result.ErrorCode,
                traceReference = result.TraceReference
            });

    private static IResult FromResult(Result<Guid> result) =>
        result.Succeeded
            ? Results.Ok(new { id = result.Data })
            : Results.BadRequest(new
            {
                message = result.Error ?? "The operation could not be completed.",
                errorCode = result.ErrorCode,
                traceReference = result.TraceReference
            });
}

public sealed class ReactCatalogBootstrapDto
{
    public IReadOnlyList<ProductListItemDto> Products { get; set; } = [];
    public IReadOnlyList<LookupDto> Categories { get; set; } = [];
    public IReadOnlyList<LookupDto> Brands { get; set; } = [];
    public IReadOnlyList<LookupDto> Units { get; set; } = [];
    public IReadOnlyList<LookupDto> Taxes { get; set; } = [];
    public IReadOnlyList<ReactStoreOptionDto> Stores { get; set; } = [];
}

public sealed class ReactInventoryBootstrapDto
{
    public IReadOnlyList<StockListItemDto> Stock { get; set; } = [];
    public IReadOnlyList<StockLedgerDto> Ledger { get; set; } = [];
    public IReadOnlyList<ProductListItemDto> Products { get; set; } = [];
}

public sealed class ReactSetProductActiveRequest
{
    public bool IsActive { get; set; }
}

public sealed class ReactProductAssignmentRequest
{
    public Guid StoreId { get; set; }
    public IReadOnlyList<Guid> ProductIds { get; set; } = [];
    public bool IsVisible { get; set; }
}

public sealed class ReactOpeningStockRequest
{
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public decimal Quantity { get; set; }
}
