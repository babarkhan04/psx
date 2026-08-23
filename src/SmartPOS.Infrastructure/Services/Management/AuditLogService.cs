using Microsoft.EntityFrameworkCore;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Infrastructure.Persistence;
using SmartPOS.Shared.Contracts;

namespace SmartPOS.Infrastructure.Services.Management;

public sealed class AuditLogService(AppDbContext db, DbContextGate gate) : IAuditLogService
{
    public Task<IReadOnlyList<AuditLogDto>> GetAsync(AuditQueryDto query, CancellationToken ct = default) =>
        gate.ExecuteAsync(() => GetCoreAsync(query, ct), ct);

    private async Task<IReadOnlyList<AuditLogDto>> GetCoreAsync(AuditQueryDto query, CancellationToken ct)
    {
        var take = query.Take <= 0 ? 200 : Math.Min(query.Take, 1000);

        var q = db.AuditLogs.AsNoTracking()
            .GroupJoin(db.Stores.AsNoTracking(),
                audit => audit.StoreId,
                store => store.Id,
                (audit, stores) => new { audit, stores })
            .SelectMany(x => x.stores.DefaultIfEmpty(),
                (x, store) => new { x.audit, store })
            .AsQueryable();

        if (query.StoreId is { } storeId && storeId != Guid.Empty)
        {
            q = q.Where(x => x.audit.StoreId == storeId);
        }

        if (!string.IsNullOrWhiteSpace(query.UserId))
        {
            var user = query.UserId.Trim();
            q = q.Where(x => x.audit.UserId == user);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            var action = query.Action.Trim();
            q = q.Where(x => x.audit.Action == action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityName))
        {
            var entityName = query.EntityName.Trim();
            q = q.Where(x => x.audit.EntityName == entityName);
        }

        if (query.FromUtc is { } fromUtc)
        {
            q = q.Where(x => x.audit.CreatedOn >= fromUtc);
        }

        if (query.ToUtc is { } toUtc)
        {
            q = q.Where(x => x.audit.CreatedOn < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(x =>
                EF.Functions.Like(x.audit.Action, $"%{term}%") ||
                EF.Functions.Like(x.audit.EntityName, $"%{term}%") ||
                (x.audit.EntityId != null && EF.Functions.Like(x.audit.EntityId, $"%{term}%")) ||
                (x.audit.Summary != null && EF.Functions.Like(x.audit.Summary, $"%{term}%")) ||
                (x.audit.UserName != null && EF.Functions.Like(x.audit.UserName, $"%{term}%")) ||
                (x.audit.OldValuesJson != null && EF.Functions.Like(x.audit.OldValuesJson, $"%{term}%")) ||
                (x.audit.NewValuesJson != null && EF.Functions.Like(x.audit.NewValuesJson, $"%{term}%")));
        }

        return await q.OrderByDescending(x => x.audit.CreatedOn)
            .Take(take)
            .Select(x => new AuditLogDto
            {
                Id = x.audit.Id,
                TenantId = x.audit.TenantId,
                StoreId = x.audit.StoreId,
                StoreName = x.store == null ? null : x.store.Code + " · " + x.store.Name,
                UserId = x.audit.UserId,
                UserName = x.audit.UserName,
                Action = x.audit.Action,
                EntityName = x.audit.EntityName,
                EntityId = x.audit.EntityId,
                Summary = x.audit.Summary,
                OldValuesJson = x.audit.OldValuesJson,
                NewValuesJson = x.audit.NewValuesJson,
                IpAddress = x.audit.IpAddress,
                UserAgent = x.audit.UserAgent,
                CorrelationId = x.audit.CorrelationId,
                CreatedOn = x.audit.CreatedOn
            })
            .ToListAsync(ct);
    }
}
