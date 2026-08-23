using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<NavStatsDto> GetNavStatsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GlobalSearchResultDto>> SearchAsync(string query, int take = 12, CancellationToken ct = default);
}
