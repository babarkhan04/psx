using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public class ReceiptTemplateDto
{
    public Guid? Id { get; set; }
    public Guid? StoreId { get; set; }
    public string? StoreName { get; set; }

    [Required, StringLength(120)]
    public string Name { get; set; } = "Default Receipt";

    public bool IsDefault { get; set; } = true;
    public bool IsActive { get; set; } = true;

    [Required, StringLength(20)]
    public string PaperSize { get; set; } = "80mm";

    [StringLength(160)]
    public string? BusinessName { get; set; }

    [StringLength(160)]
    public string? Subtitle { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    public string? HeaderText { get; set; }

    [StringLength(500)]
    public string? FooterText { get; set; }

    [StringLength(600)]
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

    [Range(1, 5)]
    public int Copies { get; set; } = 1;

    public bool AutoPrintAfterSale { get; set; }
}

public class UpdateReceiptTemplateDto : ReceiptTemplateDto
{
}

public class PrintStationSettingDto
{
    public Guid? Id { get; set; }

    [Required]
    public Guid StoreId { get; set; }

    public string? StoreName { get; set; }

    public Guid? RegisterId { get; set; }
    public string? RegisterName { get; set; }

    [Required, StringLength(120)]
    public string StationName { get; set; } = "Default POS Station";

    public Guid? ReceiptTemplateId { get; set; }

    [Required, StringLength(20)]
    public string DefaultReceiptMode { get; set; } = "thermal";

    public bool UseBrowserPrint { get; set; } = true;
    public bool AutoPrintReceipt { get; set; }

    [Range(1, 5)]
    public int ReceiptCopies { get; set; } = 1;

    [StringLength(160)]
    public string? ReceiptPrinterName { get; set; }

    [StringLength(160)]
    public string? LabelPrinterName { get; set; }

    public bool UseLocalPrintAgent { get; set; }

    [StringLength(250)]
    public string? LocalPrintAgentUrl { get; set; } = "http://localhost:3859";

    public bool EnableCashDrawerKick { get; set; }

    [StringLength(100)]
    public string? CashDrawerCommandHex { get; set; } = "1B700019FA";
}

public class UpdatePrintStationSettingDto : PrintStationSettingDto
{
}

public class PrintSetupDto
{
    public ReceiptTemplateDto Template { get; set; } = new();
    public PrintStationSettingDto Station { get; set; } = new();
    public IReadOnlyList<ReceiptTemplateDto> AvailableTemplates { get; set; } = [];
}
