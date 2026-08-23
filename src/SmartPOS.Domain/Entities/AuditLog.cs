using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Immutable audit record for important entity changes and sensitive POS actions.
/// This entity intentionally does not inherit AuditableEntity to avoid recursive audit updates.
/// </summary>
public class AuditLog : BaseEntity, ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid? StoreId { get; set; }
    public string? UserId { get; set; }
    public string? UserName { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Summary { get; set; }
    public string? OldValuesJson { get; set; }
    public string? NewValuesJson { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? CorrelationId { get; set; }
    public DateTime CreatedOn { get; set; }
}
