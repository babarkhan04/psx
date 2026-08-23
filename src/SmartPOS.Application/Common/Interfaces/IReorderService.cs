using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IReorderService
{
    Task<ReorderDashboardDto> GetReorderDashboardAsync(ReorderQueryDto query, CancellationToken ct = default);
    Task<Result<Guid>> CreatePurchaseDraftAsync(CreateReorderPurchaseDraftDto dto, CancellationToken ct = default);
    Task<Result<Guid>> CreateTransferRequestAsync(CreateReorderTransferRequestDto dto, CancellationToken ct = default);
}
