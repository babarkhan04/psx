using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IStockTransferService
{
    Task<IReadOnlyList<StockTransferListItemDto>> GetTransfersAsync(StockTransferQueryDto query, CancellationToken ct = default);
    Task<StockTransferDetailDto?> GetTransferAsync(Guid transferId, CancellationToken ct = default);
    Task<Result<Guid>> CreateRequestedAsync(CreateStockTransferDto dto, CancellationToken ct = default);
    Task<Result> ApproveAsync(StockTransferActionDto dto, CancellationToken ct = default);
    Task<Result> DispatchAsync(StockTransferActionDto dto, CancellationToken ct = default);
    Task<Result> ReceiveAsync(ReceiveStockTransferDto dto, CancellationToken ct = default);
    Task<Result> CancelAsync(StockTransferActionDto dto, CancellationToken ct = default);
}
