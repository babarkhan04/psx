using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmartPOS.Application.Common.Interfaces;
using SmartPOS.Domain.Common;
using SmartPOS.Domain.Entities;

namespace SmartPOS.Infrastructure.Persistence.Interceptors;

public sealed class AuditSaveChangesInterceptor(
    ITenantContext tenantContext,
    IStoreContext storeContext,
    ICurrentUserService currentUser,
    ICorrelationContext correlationContext,
    IDateTimeProvider clock,
    IHttpContextAccessor httpContextAccessor) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private static readonly HashSet<string> IgnoredEntityNames = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(AuditLog),
        nameof(ErrorLog),
        nameof(NumberSequence)
    };

    private static readonly HashSet<string> NoisyProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(IAuditableEntity.CreatedOn),
        nameof(IAuditableEntity.CreatedBy),
        nameof(IAuditableEntity.ModifiedOn),
        nameof(IAuditableEntity.ModifiedBy)
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddAuditEntries(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void AddAuditEntries(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker.Entries()
            .Where(ShouldAudit)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        foreach (var entry in entries)
        {
            var action = ResolveAction(entry);
            var values = ResolveValues(entry);

            if (entry.State == EntityState.Modified && values.OldValues.Count == 0 && values.NewValues.Count == 0)
            {
                continue;
            }

            var audit = new AuditLog
            {
                TenantId = ResolveTenantId(entry),
                StoreId = ResolveStoreId(entry),
                UserId = currentUser.UserId,
                UserName = currentUser.UserName ?? currentUser.Email,
                Action = action,
                EntityName = entry.Metadata.ClrType.Name,
                EntityId = ResolveEntityId(entry),
                Summary = BuildSummary(action, entry.Metadata.ClrType.Name, ResolveEntityId(entry)),
                OldValuesJson = values.OldValues.Count == 0 ? null : JsonSerializer.Serialize(values.OldValues, JsonOptions),
                NewValuesJson = values.NewValues.Count == 0 ? null : JsonSerializer.Serialize(values.NewValues, JsonOptions),
                IpAddress = httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
                CorrelationId = correlationContext.CorrelationId,
                CreatedOn = clock.UtcNow
            };

            context.Set<AuditLog>().Add(audit);
        }
    }

    private static bool ShouldAudit(EntityEntry entry)
    {
        if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return false;
        }

        var entityName = entry.Metadata.ClrType.Name;
        if (IgnoredEntityNames.Contains(entityName))
        {
            return false;
        }

        // Audit SmartPOS domain entities. Identity changes can be audited later with explicit user-admin logs.
        return entry.Entity is ITenantEntity || entry.Entity is IStoreScopedEntity;
    }

    private static string ResolveAction(EntityEntry entry)
    {
        if (entry.State == EntityState.Added)
        {
            return "Create";
        }

        if (entry.State == EntityState.Deleted)
        {
            return "Delete";
        }

        if (entry.Entity is ISoftDelete softDelete && softDelete.IsDeleted)
        {
            var isDeletedProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == nameof(ISoftDelete.IsDeleted));
            if (isDeletedProperty is not null && isDeletedProperty.IsModified)
            {
                return "Delete";
            }
        }

        return "Update";
    }

    private static string? ResolveEntityId(EntityEntry entry)
    {
        var keyProperty = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
        var keyValue = keyProperty?.CurrentValue ?? keyProperty?.OriginalValue;
        return keyValue?.ToString();
    }

    private Guid ResolveTenantId(EntityEntry entry)
    {
        if (entry.Entity is ITenantEntity tenantEntity && tenantEntity.TenantId != Guid.Empty)
        {
            return tenantEntity.TenantId;
        }

        return tenantContext.TenantId ?? Guid.Empty;
    }

    private Guid? ResolveStoreId(EntityEntry entry)
    {
        if (entry.Entity is IStoreScopedEntity storeScopedEntity && storeScopedEntity.StoreId != Guid.Empty)
        {
            return storeScopedEntity.StoreId;
        }

        return storeContext.StoreId;
    }

    private static (Dictionary<string, object?> OldValues, Dictionary<string, object?> NewValues) ResolveValues(EntityEntry entry)
    {
        var oldValues = new Dictionary<string, object?>();
        var newValues = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (property.Metadata.IsPrimaryKey() || NoisyProperties.Contains(name))
            {
                continue;
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    newValues[name] = property.CurrentValue;
                    break;

                case EntityState.Deleted:
                    oldValues[name] = property.OriginalValue;
                    break;

                case EntityState.Modified:
                    if (!property.IsModified)
                    {
                        continue;
                    }

                    oldValues[name] = property.OriginalValue;
                    newValues[name] = property.CurrentValue;
                    break;
            }
        }

        return (oldValues, newValues);
    }

    private static string BuildSummary(string action, string entityName, string? entityId) =>
        string.IsNullOrWhiteSpace(entityId)
            ? $"{action} {entityName}"
            : $"{action} {entityName} #{entityId}";
}
