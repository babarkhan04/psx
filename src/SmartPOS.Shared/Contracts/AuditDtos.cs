namespace SmartPOS.Shared.Contracts;

public sealed class AuditQueryDto
{
    public Guid? StoreId { get; set; }
    public string? UserId { get; set; }
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Search { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Take { get; set; } = 200;
}

public sealed class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }
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
