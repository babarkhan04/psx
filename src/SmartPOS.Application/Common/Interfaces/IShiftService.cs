using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Application.Common.Models;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IShiftService
{
    Task<ShiftContextDto?> GetActiveShiftAsync(Guid registerId, CancellationToken ct = default);
    Task<IReadOnlyList<ShiftListItemDto>> GetShiftsAsync(ShiftQueryDto query, CancellationToken ct = default);
    Task<IReadOnlyList<RegisterOptionDto>> GetRegisterOptionsAsync(Guid? storeId = null, CancellationToken ct = default);
    Task<Result<Guid>> OpenShiftAsync(OpenShiftDto dto, CancellationToken ct = default);
    Task<Result> RecordCashMovementAsync(RecordCashMovementDto dto, CancellationToken ct = default);
    Task<Result> RecordNoSaleDrawerAsync(NoSaleDrawerDto dto, CancellationToken ct = default);
    Task<Result> CloseShiftAsync(CloseShiftDto dto, CancellationToken ct = default);
    Task<ShiftReportDto?> GetReportAsync(Guid shiftId, CancellationToken ct = default);
}
