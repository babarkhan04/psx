using SmartPOS.Shared.Contracts;

namespace SmartPOS.Application.Common.Interfaces;

public interface IAuditLogService
{
    Task<IReadOnlyList<AuditLogDto>> GetAsync(AuditQueryDto query, CancellationToken ct = default);
}
