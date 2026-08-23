using SmartPOS.Domain.Common;

namespace SmartPOS.Domain.Entities;

/// <summary>
/// Per-store/register print station settings. These settings are intentionally
/// browser-friendly first, with optional local print-agent fields for future
/// ESC/POS/direct thermal printing.
/// </summary>
public class PrintStationSetting : StoreScopedEntity
{
    public Guid? RegisterId { get; set; }
    public string StationName { get; set; } = "Default POS Station";

    public Guid? ReceiptTemplateId { get; set; }
    public string DefaultReceiptMode { get; set; } = "thermal";
    public bool UseBrowserPrint { get; set; } = true;
    public bool AutoPrintReceipt { get; set; }
    public int ReceiptCopies { get; set; } = 1;

    public string? ReceiptPrinterName { get; set; }
    public string? LabelPrinterName { get; set; }

    public bool UseLocalPrintAgent { get; set; }
    public string? LocalPrintAgentUrl { get; set; } = "http://localhost:3859";

    public bool EnableCashDrawerKick { get; set; }
    public string? CashDrawerCommandHex { get; set; } = "1B700019FA";

    public Store? Store { get; set; }
    public Register? Register { get; set; }
    public ReceiptTemplate? ReceiptTemplate { get; set; }
}
