using SmartPOS.Shared.Contracts;

namespace SmartPOS.Web.ReactApi;

public sealed class ReactLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class ReactStoreOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(Code) ? Name : $"{Code} · {Name}";
}

public sealed class ReactCurrentUserDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = [];
    public IReadOnlyList<string> Permissions { get; set; } = [];
    public IReadOnlyList<ReactStoreOptionDto> Stores { get; set; } = [];
    public Guid? SelectedStoreId { get; set; }
}

public sealed class ReactSelectStoreRequest
{
    public Guid StoreId { get; set; }
}

public sealed class ReactDashboardDto
{
    public DashboardSummaryDto Summary { get; set; } = new();
    public SmartAlertSummaryDto AlertSummary { get; set; } = new();
    public IReadOnlyList<SmartAlertDto> TopAlerts { get; set; } = [];
}

public sealed class ReactPosBootstrapDto
{
    public PosContextDto? Context { get; set; }
    public PosFilterLookupsDto Filters { get; set; } = new();
    public IReadOnlyList<PosProductDto> Products { get; set; } = [];
    public IReadOnlyList<CustomerLookupDto> Customers { get; set; } = [];
    public IReadOnlyList<PosShortcutSettingDto> Shortcuts { get; set; } = [];
}


public sealed class ReactManagerApprovalRequest
{
    public Guid? StoreId { get; set; }
    public string ApprovalType { get; set; } = "Discount";
    public string ManagerEmail { get; set; } = string.Empty;
    public string ManagerPassword { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal? Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
}
