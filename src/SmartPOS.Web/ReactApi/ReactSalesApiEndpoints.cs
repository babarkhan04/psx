using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Authorization;
using SmartPOS.Domain.Enums;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Web.ReactApi;

public static class ReactSalesApiEndpoints
{
    public static RouteGroupBuilder MapReactSalesApi(this RouteGroupBuilder api)
    {
        var sales = api.MapGroup("/sales")
            .RequireAuthorization(Permissions.Sales.View);

        sales.MapGet("", GetSalesAsync);
        sales.MapGet("/{saleId:guid}/receipt", GetReceiptAsync);
        sales.MapPost("/{saleId:guid}/reprint", LogReprintAsync).DisableAntiforgery();

        sales.MapGet("/{saleId:guid}/return", GetReturnAsync)
            .RequireAuthorization(Permissions.Sales.Refund);
        sales.MapPost("/return", ProcessReturnAsync)
            .RequireAuthorization(Permissions.Sales.Refund)
            .DisableAntiforgery();
        sales.MapPost("/void", VoidSaleAsync)
            .RequireAuthorization(Permissions.Sales.Void)
            .DisableAntiforgery();
        sales.MapPost("/manager-approval", ApproveManagerActionAsync)
            .RequireAuthorization(Permissions.Sales.Void)
            .DisableAntiforgery();

        return api;
    }

    private static async Task<IResult> GetSalesAsync(
        Guid storeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        int? take,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);

        var page = await posService.GetSalesAsync(new SalesQueryDto
        {
            StoreId = storeId,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Search = search,
            Take = Math.Clamp(take ?? 250, 1, 500)
        }, ct);

        return Results.Ok(page);
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
            return Results.NotFound(new { message = "Receipt could not be found." });
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        return Results.Ok(receipt);
    }

    private static async Task<IResult> LogReprintAsync(
        Guid saleId,
        ReactReceiptReprintRequest request,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var receipt = await posService.GetReceiptAsync(saleId, ct);
        if (receipt is null)
        {
            return Results.NotFound(new { message = "Receipt could not be found." });
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        var result = await posService.LogReceiptReprintAsync(new ReceiptReprintRequestDto
        {
            SaleId = saleId,
            Reason = request.Reason
        }, ct);

        return result.Succeeded
            ? Results.NoContent()
            : Results.BadRequest(new { message = result.Error ?? "Could not log receipt reprint.", errorCode = result.ErrorCode });
    }

    private static async Task<IResult> GetReturnAsync(
        Guid saleId,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var receipt = await posService.GetReceiptAsync(saleId, ct);
        if (receipt is null)
        {
            return Results.NotFound(new { message = "Sale could not be found." });
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        var sale = await posService.GetSaleForReturnAsync(saleId, ct);
        return sale is null
            ? Results.NotFound(new { message = "This sale cannot be returned." })
            : Results.Ok(sale);
    }

    private static async Task<IResult> ProcessReturnAsync(
        ProcessReturnDto dto,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var receipt = await posService.GetReceiptAsync(dto.SaleId, ct);
        if (receipt is null)
        {
            return Results.NotFound(new { message = "Sale could not be found." });
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        var result = await posService.ProcessReturnAsync(dto, ct);
        return result.Succeeded
            ? Results.Ok(result.Data)
            : Results.BadRequest(new { message = result.Error ?? "Return could not be processed.", errorCode = result.ErrorCode });
    }

    private static async Task<IResult> VoidSaleAsync(
        VoidSaleDto dto,
        IPosService posService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        var receipt = await posService.GetReceiptAsync(dto.SaleId, ct);
        if (receipt is null)
        {
            return Results.NotFound(new { message = "Sale could not be found." });
        }

        await storeAccess.EnsureCanAccessStoreAsync(receipt.StoreId, ct);
        var result = await posService.VoidSaleAsync(dto, ct);
        return result.Succeeded
            ? Results.NoContent()
            : Results.BadRequest(new { message = result.Error ?? "Sale could not be voided.", errorCode = result.ErrorCode });
    }
    private static async Task<IResult> ApproveManagerActionAsync(
        ReactManagerApprovalRequest request,
        IManagerApprovalService managerApprovalService,
        IStoreAccessService storeAccess,
        CancellationToken ct)
    {
        if (request.StoreId is { } storeId)
        {
            await storeAccess.EnsureCanAccessStoreAsync(storeId, ct);
        }

        var result = await managerApprovalService.ApproveAsync(new ManagerApprovalRequestDto
        {
            StoreId = request.StoreId,
            ApprovalType = ManagerApprovalType.VoidSale,
            ManagerEmail = request.ManagerEmail,
            ManagerPassword = request.ManagerPassword,
            Reference = request.Reference,
            Amount = request.Amount,
            Reason = request.Reason
        }, ct);

        return result.Succeeded
            ? Results.Ok(result.Data)
            : Results.BadRequest(new { message = result.Error ?? "Manager approval failed.", errorCode = result.ErrorCode });
    }

}

public sealed class ReactReceiptReprintRequest
{
    public string Reason { get; set; } = string.Empty;
}
