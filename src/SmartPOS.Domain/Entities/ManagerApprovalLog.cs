using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Stores manager approvals for sensitive POS actions such as discounts, voids, refunds,
/// price overrides, cash variance, and stock adjustments.
/// </summary>
public class ManagerApprovalLog : AuditableEntity
{
    public Guid? StoreId { get; set; }
    public ManagerApprovalType ApprovalType { get; set; }
    public ManagerApprovalStatus Status { get; set; } = ManagerApprovalStatus.Approved;

    public string RequestedByUserId { get; set; } = string.Empty;
    public string? RequestedByUserName { get; set; }

    public string ApprovedByUserId { get; set; } = string.Empty;
    public string ApprovedByUserName { get; set; } = string.Empty;

    public string? Reference { get; set; }
    public decimal? Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public DateTime ApprovedOn { get; set; }
}
