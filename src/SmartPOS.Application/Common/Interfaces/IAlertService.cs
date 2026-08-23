using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IAlertService
{
    Task<SmartAlertsPageDto> GetAlertsAsync(SmartAlertQueryDto query, CancellationToken ct = default);
}
