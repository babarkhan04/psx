using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IRiskService
{
    Task<RiskDashboardDto> GetRiskDashboardAsync(RiskQueryDto query, CancellationToken ct = default);
}
