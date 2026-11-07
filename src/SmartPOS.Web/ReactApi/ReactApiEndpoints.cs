using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Enums;
using SmartPOS.Infrastructure.Identity;
using SmartPOS.Infrastructure.Services;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Web.ReactApi;

public static class ReactApiEndpoints
{
    public static IEndpointRouteBuilder MapReactApi(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/react/auth/login", LoginAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        app.MapPost("/api/react/auth/logout", LogoutAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        var api = app.MapGroup("/api/react")
            .RequireAuthorization();

        api.MapGet("/auth/me", GetCurrentUserAsync);
        api.MapPost("/stores/select", SelectStoreAsync).DisableAntiforgery();

        api.MapGet("/dashboard", GetDashboardAsync);

        var pos = api.MapGroup("/pos")
            .RequireAuthorization(Permissions.Sales.Operate);

        pos.MapGet("/bootstrap", GetPosBootstrapAsync);
        pos.MapGet("/products", GetPosProductsAsync);
        pos.MapGet("/barcode", LookupBarcodeAsync);
        pos.MapPost("/favorites/{productId:guid}", ToggleFavoriteAsync).DisableAntiforgery();
        pos.MapPost("/checkout", CheckoutAsync).DisableAntiforgery();
        pos.MapPost("/hold", HoldSaleAsync).DisableAntiforgery();
        pos.MapGet("/held", GetHeldSalesAsync);
        pos.MapGet("/held/{heldSaleId:guid}", GetHeldSaleAsync);
        pos.MapDelete("/held/{heldSaleId:guid}", CancelHeldSaleAsync).DisableAntiforgery();
        pos.MapGet("/receipt/{saleId:guid}", GetReceiptAsync);
        pos.MapPost("/manager-approval", ApproveManagerActionAsync).DisableAntiforgery();

        var shifts = api.MapGroup("/shifts")
            .RequireAuthorization(Permissions.Operations.ShiftsView);

        shifts.MapGet("", GetShiftsAsync);
        shifts.MapGet("/registers", GetShiftRegistersAsync);
        shifts.MapPost("/open", OpenShiftAsync)
            .RequireAuthorization(Permissions.Operations.ShiftsOpen)
            .DisableAntiforgery();
        shifts.MapPost("/cash-movement", RecordShiftCashMovementAsync)
            .RequireAuthorization(Permissions.Operations.ShiftsCashMove)
            .DisableAntiforgery();
        shifts.MapPost("/no-sale", RecordNoSaleAsync)
            .RequireAuthorization(Permissions.Operations.ShiftsCashMove)
            .DisableAntiforgery();
        shifts.MapPost("/close", CloseShiftAsync)
            .RequireAuthorization(Permissions.Operations.ShiftsClose)
            .DisableAntiforgery();
        shifts.MapGet("/{shiftId:guid}/report", GetShiftReportAsync);

        api.MapReactSalesApi();
        api.MapReactCatalogInventoryApi();

        return app;
    }

    private static async Task<IResult> LoginAsync(
        ReactLoginRequest request,
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IStoreService storeService,
        IStoreAccessService storeAccess,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var email = request.Email?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new { message = "Email and password are required." });
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !user.IsActive)
        {
            return Results.Unauthorized();
        }

        var result = await signInManager.PasswordSignInAsync(
            user.UserName!,
            request.Password,
            isPersistent: true,
            lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            return result.IsLockedOut
                ? Results.Json(new { message = "Your account is locked. Please contact your administrator." }, statusCode: StatusCodes.Status423Locked)
                : Results.Unauthorized();
        }

        user.LastLoginOn = DateTime.UtcNow;
        await userManager.UpdateAsync(user);

        // PasswordSignIn writes the cookie to this response, but HttpContext.User is
        // still the anonymous principal for the current request. Return a compact
        // login response; React immediately calls /auth/me on the next request.
        return Results.Ok(new
        {
            signedIn = true,
            user.DisplayName,
            user.Email
        });
    }

    private static async Task<IResult> LogoutAsync(
        SignInManager<ApplicationUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.NoContent();
    }

    private static async Task<IResult> GetCurrentUserAsync(
        HttpContext httpContext,
        ICurrentUserService currentUser,
        IStoreService storeService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        CancellationToken ct)
    {
        if (!currentUser.IsAuthenticated || string.IsNullOrWhiteSpace(currentUser.UserId))
        {
            return Results.Unauthorized();
        }

        var allowedIds = await storeAccess.GetAllowedStoreIdsAsync(ct);
        var allowedSet = allowedIds.ToHashSet();
        var stores = (await storeService.GetAllAsync(includeInactive: false, ct))
            .Where(s => allowedSet.Contains(s.Id))
            .Select(s => new ReactStoreOptionDto
            {
                Id = s.Id,
                Code = s.Code,
                Name = s.Name
            })
            .ToList();

        var selected = storeContext.StoreId;
        if (selected is null || !allowedSet.Contains(selected.Value))
        {
            selected = await storeAccess.GetDefaultStoreIdAsync(ct);
        }

        if (selected is { } selectedStore)
        {
            storeContext.SetStore(selectedStore);
        }

        var displayName =
            httpContext.User.FindFirstValue(AppClaimTypes.DisplayName)
            ?? currentUser.UserName
            ?? currentUser.Email
            ?? "User";

        var permissions = httpContext.User.FindAll(Permissions.ClaimType)
            .Select(c => c.Value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x)
            .ToList();

        return Results.Ok(new ReactCurrentUserDto
        {
            Id = currentUser.UserId!,
            Email = currentUser.Email ?? string.Empty,
            DisplayName = displayName,
            Roles = currentUser.Roles,
            Permissions = permissions,
            Stores = stores,
            SelectedStoreId = selected
        });
    }

    private static async Task<IResult> SelectStoreAsync(
        ReactSelectStoreRequest request,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(request.StoreId, ct);
        storeContext.SetStore(request.StoreId);
        SetStoreCookie(httpContext.Response, request.StoreId);
        return Results.NoContent();
    }

    private static async Task<IResult> GetDashboardAsync(
        Guid? storeId,
        IDashboardService dashboardService,
        IAlertService alertService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var selected = await ResolveStoreAsync(storeId, storeAccess, storeContext, httpContext.Response, ct);
        if (selected is null)
        {
            return Results.BadRequest(new { message = "No accessible store is available." });
        }

        var summary = await dashboardService.GetSummaryAsync(ct);
        var alerts = await alertService.GetAlertsAsync(new SmartAlertQueryDto
        {
            StoreId = selected,
            IncludeInfo = true
        }, ct);

        return Results.Ok(new ReactDashboardDto
        {
            Summary = summary,
            AlertSummary = alerts.Summary,
            TopAlerts = alerts.Alerts.Take(6).ToList()
        });
    }

    private static async Task<IResult> GetPosBootstrapAsync(
        Guid? storeId,
        IPosService posService,
        IPosShortcutService shortcutService,
        ICustomerService customerService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var selected = await ResolveStoreAsync(storeId, storeAccess, storeContext, httpContext.Response, ct);
        if (selected is null)
        {
            return Results.BadRequest(new { message = "No accessible store is available." });
        }

        var context = await posService.GetContextAsync(selected.Value, ct);
        if (context is null)
        {
            return Results.Ok(new ReactPosBootstrapDto
            {
                Context = null,
                Filters = new PosFilterLookupsDto(),
                Products = [],
                Customers = [],
                Shortcuts = []
            });
        }

        var filters = await posService.GetProductFilterLookupsAsync(selected.Value, ct);
        var products = await posService.GetProductsAsync(selected.Value, ct: ct);
        var customers = await customerService.SearchLookupAsync(null, 200, ct);
        var shortcuts = await shortcutService.GetAsync(ct);

        return Results.Ok(new ReactPosBootstrapDto
        {
            Context = context,
            Filters = filters,
            Products = products,
            Customers = customers,
            Shortcuts = shortcuts
        });
    }

    private static async Task<IResult> GetPosProductsAsync(
        Guid storeId,
        Guid? categoryId,
        Guid? brandId,
        string? search,
        IPosService posService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        storeContext.SetStore(storeId);
        SetStoreCookie(httpContext.Response, storeId);
        var products = await posService.GetProductsAsync(storeId, categoryId, brandId, search, ct);
        return Results.Ok(products);
    }

    private static async Task<IResult> LookupBarcodeAsync(
        Guid storeId,
        string code,
        IPosService posService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        storeContext.SetStore(storeId);
        var product = await posService.LookupBarcodeAsync(storeId, code, ct);
        return product is null ? Results.NotFound() : Results.Ok(product);
    }

    private static async Task<IResult> ToggleFavoriteAsync(
        Guid productId,
        bool isFavorite,
        IPosService posService,
        CancellationToken ct)
    {
        var result = await posService.ToggleProductFavoriteAsync(productId, isFavorite, ct);
        return FromResult(result);
    }

    private static async Task<IResult> CheckoutAsync(
        PosCheckoutDto dto,
        IPosService posService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(dto.StoreId, ct);
        storeContext.SetStore(dto.StoreId);
        SetStoreCookie(httpContext.Response, dto.StoreId);
        var result = await posService.CompleteSaleAsync(dto, ct);
        return FromResult(result);
    }

    private static async Task<IResult> HoldSaleAsync(
        HoldSaleDto dto,
        IPosService posService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(dto.StoreId, ct);
        storeContext.SetStore(dto.StoreId);
        SetStoreCookie(httpContext.Response, dto.StoreId);
        var result = await posService.HoldSaleAsync(dto, ct);
        return FromResult(result);
    }

    private static async Task<IResult> GetHeldSalesAsync(
        Guid storeId,
        Guid registerId,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        return Results.Ok(await posService.GetHeldSalesAsync(storeId, registerId, ct));
    }

    private static async Task<IResult> GetHeldSaleAsync(
        Guid heldSaleId,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var sale = await posService.GetHeldSaleAsync(heldSaleId, ct);
        if (sale is null)
        {
            return Results.NotFound();
        }

        await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);
        return Results.Ok(sale);
    }

    private static async Task<IResult> CancelHeldSaleAsync(
        Guid heldSaleId,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var sale = await posService.GetHeldSaleAsync(heldSaleId, ct);
        if (sale is null)
        {
            return Results.NotFound();
        }

        await storeAccess.EnsureCanAccessStoreAsync(sale.StoreId, ct);
        var result = await posService.CancelHeldSaleAsync(heldSaleId, ct);
        return FromResult(result);
    }

    private static async Task<IResult> GetReceiptAsync(
        Guid saleId,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var receipt = await posService.GetReceiptAsync(saleId, ct);
        if (receipt is null)
        {
            return Results.NotFound();
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        return Results.Ok(receipt);
    }

    private static async Task<IResult> ApproveManagerActionAsync(
        ReactManagerApprovalRequest request,
        IManagerApprovalService managerApprovalService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (!Enum.TryParse<ManagerApprovalType>(request.ApprovalType, true, out var approvalType))
        {
            return Results.BadRequest(new { message = "Invalid approval type." });
        }

        if (request.StoreId is { } storeId)
        {
            await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        }

        var result = await managerApprovalService.ApproveAsync(new ManagerApprovalRequestDto
        {
            StoreId = request.StoreId,
            ApprovalType = approvalType,
            ManagerEmail = request.ManagerEmail,
            ManagerPassword = request.ManagerPassword,
            Reference = request.Reference,
            Amount = request.Amount,
            Reason = request.Reason
        }, ct);

        return FromResult(result);
    }

    private static async Task<IResult> GetShiftsAsync(
        Guid storeId,
        bool openOnly,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        storeContext.SetStore(storeId);
        SetStoreCookie(httpContext.Response, storeId);

        var shifts = await shiftService.GetShiftsAsync(new ShiftQueryDto
        {
            StoreId = storeId,
            OpenOnly = openOnly,
            Take = 500
        }, ct);

        return Results.Ok(shifts);
    }

    private static async Task<IResult> GetShiftRegistersAsync(
        Guid storeId,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        return Results.Ok(await shiftService.GetRegisterOptionsAsync(storeId, ct));
    }

    private static async Task<IResult> OpenShiftAsync(
        Guid storeId,
        OpenShiftDto dto,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        var registers = await shiftService.GetRegisterOptionsAsync(storeId, ct);
        if (!registers.Any(r => r.Id == dto.RegisterId && r.StoreId == storeId))
        {
            return Results.BadRequest(new { message = "Register does not belong to the selected store." });
        }

        storeContext.SetStore(storeId);
        SetStoreCookie(httpContext.Response, storeId);
        return FromResult(await shiftService.OpenShiftAsync(dto, ct));
    }

    private static async Task<IResult> RecordShiftCashMovementAsync(
        Guid storeId,
        RecordCashMovementDto dto,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (!await CanAccessShiftAsync(storeId, dto.ShiftId, shiftService, storeAccess, ct))
        {
            return Results.NotFound();
        }

        return FromResult(await shiftService.RecordCashMovementAsync(dto, ct));
    }

    private static async Task<IResult> RecordNoSaleAsync(
        Guid storeId,
        NoSaleDrawerDto dto,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (!await CanAccessShiftAsync(storeId, dto.ShiftId, shiftService, storeAccess, ct))
        {
            return Results.NotFound();
        }

        return FromResult(await shiftService.RecordNoSaleDrawerAsync(dto, ct));
    }

    private static async Task<IResult> CloseShiftAsync(
        Guid storeId,
        CloseShiftDto dto,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (!await CanAccessShiftAsync(storeId, dto.ShiftId, shiftService, storeAccess, ct))
        {
            return Results.NotFound();
        }

        return FromResult(await shiftService.CloseShiftAsync(dto, ct));
    }

    private static async Task<IResult> GetShiftReportAsync(
        Guid shiftId,
        Guid storeId,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (!await CanAccessShiftAsync(storeId, shiftId, shiftService, storeAccess, ct))
        {
            return Results.NotFound();
        }

        var report = await shiftService.GetReportAsync(shiftId, ct);
        return report is null ? Results.NotFound() : Results.Ok(report);
    }

    private static async Task<bool> CanAccessShiftAsync(
        Guid storeId,
        Guid shiftId,
        IShiftService shiftService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        var candidates = await shiftService.GetShiftsAsync(new ShiftQueryDto
        {
            StoreId = storeId,
            Take = 5000
        }, ct);
        return candidates.Any(s => s.Id == shiftId);
    }

    private static async Task<Guid?> ResolveStoreAsync(
        Guid? requestedStoreId,
        IStoreAccessService storeAccess,
        IStoreContext storeContext,
        HttpResponse response,
        CancellationToken ct)
    {
        var selected = requestedStoreId ?? storeContext.StoreId;
        if (selected is null)
        {
            selected = await storeAccess.GetDefaultStoreIdAsync(ct);
        }

        if (selected is null)
        {
            return null;
        }

        await storeAccess.EnsureCanAccessStoreAsync(selected.Value, ct);
        storeContext.SetStore(selected.Value);
        SetStoreCookie(response, selected.Value);
        return selected;
    }

    private static void SetStoreCookie(HttpResponse response, Guid storeId)
    {
        response.Cookies.Append(
            StoreContext.SelectedStoreCookieName,
            storeId.ToString(),
            new CookieOptions
            {
                HttpOnly = false,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = true,
                MaxAge = TimeSpan.FromDays(30),
                Path = "/"
            });
    }

    private static IResult FromResult(Result result)
    {
        if (result.Succeeded)
        {
            return Results.NoContent();
        }

        return Results.BadRequest(new
        {
            message = result.Error ?? "The operation could not be completed.",
            errorCode = result.ErrorCode,
            traceReference = result.TraceReference
        });
    }

    private static IResult FromResult<T>(Result<T> result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(result.Data);
        }

        return Results.BadRequest(new
        {
            message = result.Error ?? "The operation could not be completed.",
            errorCode = result.ErrorCode,
            traceReference = result.TraceReference
        });
    }
}
