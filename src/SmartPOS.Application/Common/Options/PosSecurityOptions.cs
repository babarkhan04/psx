namespace SmartPOS.Application.Common.Options;

public sealed class PosSecurityOptions
{
    public bool RequireDiscountManagerApproval { get; set; }
    public int ApprovalExpiryMinutes { get; set; } = 15;
}
