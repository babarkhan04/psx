using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IPosService
{
    Task<PosContextDto?> GetContextAsync(Guid storeId, CancellationToken ct = default);
    Task<IReadOnlyList<PosProductDto>> GetProductsAsync(
        Guid storeId,
        Guid? categoryId = null,
        Guid? brandId = null,
        string? search = null,
        CancellationToken ct = default);
    Task<PosFilterLookupsDto> GetProductFilterLookupsAsync(Guid storeId, CancellationToken ct = default);
    Task<PosProductDto?> LookupBarcodeAsync(Guid storeId, string code, CancellationToken ct = default);
    Task<Result<PosReceiptDto>> CompleteSaleAsync(PosCheckoutDto checkout, CancellationToken ct = default);
    Task<Result<Guid>> HoldSaleAsync(HoldSaleDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<HeldSaleListItemDto>> GetHeldSalesAsync(Guid storeId, Guid registerId, CancellationToken ct = default);
    Task<HeldSaleDto?> GetHeldSaleAsync(Guid heldSaleId, CancellationToken ct = default);
    Task<Result> CancelHeldSaleAsync(Guid heldSaleId, CancellationToken ct = default);
    Task<PosReceiptDto?> GetReceiptAsync(Guid saleId, CancellationToken ct = default);
    Task<Result> LogReceiptReprintAsync(ReceiptReprintRequestDto dto, CancellationToken ct = default);
    Task<Result> ToggleProductFavoriteAsync(Guid productId, bool isFavorite, CancellationToken ct = default);
    Task<SalesPageDto> GetSalesAsync(SalesQueryDto query, CancellationToken ct = default);
    Task<SaleForReturnDto?> GetSaleForReturnAsync(Guid saleId, CancellationToken ct = default);
    Task<Result> VoidSaleAsync(VoidSaleDto dto, CancellationToken ct = default);
    Task<Result<SaleReturnReceiptDto>> ProcessReturnAsync(ProcessReturnDto dto, CancellationToken ct = default);
}
public interface IPosShortcutService
{
    Task<IReadOnlyList<PosShortcutSettingDto>> GetAsync(CancellationToken ct = default);
    Task<Result> SaveAsync(IReadOnlyList<UpdatePosShortcutDto> shortcuts, CancellationToken ct = default);
    Task<Result> ResetDefaultsAsync(CancellationToken ct = default);
}
