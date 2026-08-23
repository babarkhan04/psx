using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IPurchaseService
{
    Task<IReadOnlyList<PurchaseListItemDto>> GetAllAsync(PurchaseQueryDto query, CancellationToken ct = default);
    Task<PurchaseDetailDto?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Result<Guid>> CreateDraftAsync(PurchaseEditDto dto, CancellationToken ct = default);
    Task<Result> ReceiveAsync(Guid purchaseId, CancellationToken ct = default);
    Task<Result> CancelAsync(Guid purchaseId, CancellationToken ct = default);
    Task<Result<Guid>> CreateAndReceiveAsync(PurchaseEditDto dto, CancellationToken ct = default);
}
