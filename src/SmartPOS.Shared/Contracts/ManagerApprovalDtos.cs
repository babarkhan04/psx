using System.ComponentModel.DataAnnotations;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Shared.Contracts;

public sealed class ManagerApprovalRequestDto
{
    public Guid? StoreId { get; set; }

    [Required]
    public ManagerApprovalType ApprovalType { get; set; } = ManagerApprovalType.Discount;

    [Required]
    [EmailAddress]
    public string ManagerEmail { get; set; } = string.Empty;

    [Required]
    public string ManagerPassword { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Reference { get; set; }

    public decimal? Amount { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ManagerApprovalResultDto
{
    public Guid ApprovalId { get; set; }
    public string ApprovedByUserId { get; set; } = string.Empty;
    public string ApprovedByName { get; set; } = string.Empty;
    public DateTime ApprovedOn { get; set; }
}

public sealed class ManagerApprovalLogDto
{
    public Guid Id { get; set; }
    public DateTime ApprovedOn { get; set; }
    public string ApprovalType { get; set; } = string.Empty;
    public string ApprovedByName { get; set; } = string.Empty;
    public string? RequestedByUserName { get; set; }
    public string? Reference { get; set; }
    public decimal? Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
