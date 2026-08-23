using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IReportService
{
    Task<ReportsPageDto> GetReportsAsync(ReportQueryDto query, CancellationToken ct = default);

    Task<byte[]> ExportReportsWorkbookAsync(ReportQueryDto query, CancellationToken ct = default);
}
