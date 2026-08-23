using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<StockListItemDto>> GetStockAsync(Guid? storeId = null, bool lowStockOnly = false, CancellationToken ct = default);
    Task<IReadOnlyList<StockLedgerDto>> GetLedgerAsync(Guid? storeId = null, Guid? productId = null, int take = 100, CancellationToken ct = default);
    Task<Result> PostAdjustmentAsync(StockAdjustmentDto dto, CancellationToken ct = default);
    Task<Result> PostOpeningStockAsync(Guid storeId, Guid productId, decimal quantity, Guid? variantId = null, CancellationToken ct = default);
}
