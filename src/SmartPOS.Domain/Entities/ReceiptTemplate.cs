using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Store-aware receipt template used by the POS receipt dialog and print layout.
/// A null StoreId means the template is the tenant-wide default.
/// </summary>
public class ReceiptTemplate : AuditableEntity
{
    public Guid? StoreId { get; set; }
    public string Name { get; set; } = "Default Receipt";
    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public string PaperSize { get; set; } = "80mm";

    public string? BusinessName { get; set; }
    public string? Subtitle { get; set; }
    public string? LogoUrl { get; set; }
    public string? HeaderText { get; set; }
    public string? FooterText { get; set; }
    public string? ReturnPolicyText { get; set; }

    public bool ShowStoreName { get; set; } = true;
    public bool ShowCustomer { get; set; } = true;
    public bool ShowCashier { get; set; } = true;
    public bool ShowPaymentSummary { get; set; } = true;
    public bool ShowTaxDetails { get; set; } = true;
    public bool ShowItemSku { get; set; } = true;
    public bool ShowDiscountDetails { get; set; } = true;
    public bool ShowPrintedBy { get; set; } = true;
    public bool ShowReceiptNumberBox { get; set; } = true;

    public int Copies { get; set; } = 1;
    public bool AutoPrintAfterSale { get; set; }

    public Store? Store { get; set; }
}
