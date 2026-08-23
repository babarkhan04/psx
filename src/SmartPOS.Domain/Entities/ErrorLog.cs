using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

public class ErrorLog : BaseEntity
{
    public string CorrelationId { get; set; } = string.Empty;
    public string TraceId { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public Guid? StoreId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string ErrorCode { get; set; } = "UNHANDLED";
    public string Message { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? StackTrace { get; set; }
    public string? RequestPath { get; set; }
    public string? HttpMethod { get; set; }
    public int? StatusCode { get; set; }
    public string Severity { get; set; } = "Error";
    public DateTime OccurredOn { get; set; }
    public bool IsResolved { get; set; }
}
