using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IManagerApprovalService
{
    Task<Result<ManagerApprovalResultDto>> ApproveAsync(ManagerApprovalRequestDto request, CancellationToken ct = default);
    Task<IReadOnlyList<ManagerApprovalLogDto>> GetRecentAsync(Guid? storeId = null, int take = 100, CancellationToken ct = default);
}
