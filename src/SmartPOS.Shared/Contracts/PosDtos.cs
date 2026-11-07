using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class PosProductDto
{
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal StockOnHand { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string? UnitAbbreviation { get; set; }
    public bool IsFavorite { get; set; }
    public int FavoriteSortOrder { get; set; }
    public bool IsOutOfStock => StockOnHand <= 0;
}

public sealed class PosCartLineDto
{
    public Guid? ManagerApprovalId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineGross => Math.Round(Quantity * UnitPrice, 2);
    public decimal LineDiscount => Math.Round(Math.Min(Math.Max(0m, DiscountAmount), LineGross), 2);
    public decimal LineSubtotal => Math.Round(LineGross - LineDiscount, 2);
    public decimal LineTax => Math.Round(LineSubtotal * TaxRate / 100m, 2);
    public decimal LineTotal => LineSubtotal + LineTax;
}

public sealed class PosCheckoutDto
{
    public Guid? ManagerApprovalId { get; set; }
    [Required]
    public Guid StoreId { get; set; }

    [Required]
    public Guid RegisterId { get; set; }

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-In Customer";

    /// <summary>Backward-compatible single payment method used by Cash/Card/Udhaar quick buttons.</summary>
    [Required]
    public string PaymentMethod { get; set; } = "Cash";

    /// <summary>Backward-compatible tendered amount used by the Cash quick button.</summary>
    public decimal AmountTendered { get; set; }
    public string? Notes { get; set; }

    public decimal InvoiceDiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    public Guid? HeldSaleId { get; set; }

    public IList<PosCartLineDto> Lines { get; set; } = [];

    /// <summary>When supplied, the sale is completed using multiple payment lines instead of PaymentMethod/AmountTendered.</summary>
    public IList<PosPaymentLineDto> Payments { get; set; } = [];
}

public sealed class PosPaymentLineDto
{
    [Required]
    public string Method { get; set; } = "Cash";

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? Reference { get; set; }
}

public sealed class PosDiscountResultDto
{
    public Guid? ManagerApprovalId { get; set; }
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
}

public sealed class PosReceiptDto
{
    public Guid SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public Guid? RegisterId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal ChangeDue { get; set; }
    public DateTime CompletedOn { get; set; }
    public IReadOnlyList<PosReceiptLineDto> Lines { get; set; } = [];
    public IReadOnlyList<PosReceiptPaymentDto> Payments { get; set; } = [];
    public int ReprintCount { get; set; }
    public IReadOnlyList<ReceiptReprintLogDto> ReprintLogs { get; set; } = [];
}

public sealed class PosReceiptPaymentDto
{
    public string Method { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
}

public sealed class PosReceiptLineDto
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class PosFilterLookupsDto
{
    public IReadOnlyList<LookupDto> Categories { get; set; } = [];
    public IReadOnlyList<LookupDto> Brands { get; set; } = [];
}

public sealed class PosContextDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public Guid RegisterId { get; set; }
    public string RegisterName { get; set; } = string.Empty;
    public bool RequiresShift { get; set; }
    public bool HasOpenShift { get; set; }
    public Guid? ShiftId { get; set; }
    public string? ShiftNumber { get; set; }
    public decimal? ShiftCashBalance { get; set; }
}

public sealed class SalesQueryDto
{
    public Guid? StoreId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public string? Search { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class SaleListItemDto
{
    public Guid Id { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class SalesSummaryDto
{
    public int SaleCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal TaxCollected { get; set; }
    public int ItemsSold { get; set; }
}

public sealed class SalesPageDto
{
    public IReadOnlyList<SaleListItemDto> Sales { get; set; } = [];
    public SalesSummaryDto Summary { get; set; } = new();
}

public sealed class SaleReturnLineInputDto
{
    [Required]
    public Guid SaleLineId { get; set; }

    [Range(0.001, 999999)]
    public decimal QuantityReturned { get; set; }

    /// <summary>
    /// Restock = return to sellable stock, Damaged = not sellable, Discard = write-off/no stock return.
    /// </summary>
    [Required]
    public string Disposition { get; set; } = "Restock";

    [StringLength(300)]
    public string? LineReason { get; set; }

    public bool Restock => string.Equals(Disposition, "Restock", StringComparison.OrdinalIgnoreCase);
}

public sealed class ProcessReturnDto
{
    [Required]
    public Guid SaleId { get; set; }

    [Required]
    public string ReturnType { get; set; } = "Refund";

    [Required]
    public string RefundMethod { get; set; } = "Cash";

    [Required]
    [StringLength(300, MinimumLength = 3)]
    public string ReturnReason { get; set; } = string.Empty;

    [StringLength(120)]
    public string? RefundReference { get; set; }

    [StringLength(120)]
    public string? ExchangeReference { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IList<SaleReturnLineInputDto> Lines { get; set; } = [];
}


public sealed class VoidSaleDto
{
    public Guid? ManagerApprovalId { get; set; }
    [Required]
    public Guid SaleId { get; set; }

    [Required]
    [StringLength(500, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class SaleForReturnDto
{
    public Guid SaleId { get; set; }
    public string SaleNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Guid? CustomerId { get; set; }
    public string OriginalPaymentMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CompletedOn { get; set; }
    public IReadOnlyList<SaleReturnLineOptionDto> Lines { get; set; } = [];
}

public sealed class SaleReturnLineOptionDto
{
    public decimal RefundableAmount { get; set; }
    public decimal RefundableTax { get; set; }
    public decimal RefundedAmount { get; set; }
    public decimal RefundedTax { get; set; }
    public Guid SaleLineId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal QuantityAlreadyReturned { get; set; }
    public decimal QuantityReturnable => QuantitySold - QuantityAlreadyReturned;
    public decimal UnitPrice { get; set; }
    public decimal DiscountedUnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SaleReturnReceiptDto
{
    public decimal CreditReversedAmount { get; set; }
    public decimal SettlementAmount { get; set; }
    public Guid ReturnId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public string OriginalSaleNumber { get; set; } = string.Empty;
    public string ReturnType { get; set; } = string.Empty;
    public string RefundMethod { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ReturnReason { get; set; } = string.Empty;
    public string? RefundReference { get; set; }
    public string? ExchangeReference { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime ProcessedOn { get; set; }
    public IReadOnlyList<SaleReturnReceiptLineDto> Lines { get; set; } = [];
}

public sealed class SaleReturnReceiptLineDto
{
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal LineTotal { get; set; }
    public string Disposition { get; set; } = "Restock";
    public string? LineReason { get; set; }
    public bool Restock => string.Equals(Disposition, "Restock", StringComparison.OrdinalIgnoreCase);
}

public sealed class HoldSaleDto
{
    [Required]
    public Guid StoreId { get; set; }

    [Required]
    public Guid RegisterId { get; set; }

    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-In Customer";

    [StringLength(500)]
    public string? Notes { get; set; }

    public decimal InvoiceDiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    /// <summary>Optional override for how long this sale stays parked. 0 uses the system default.</summary>
    public int ExpireAfterMinutes { get; set; }

    public IList<PosCartLineDto> Lines { get; set; } = [];
}

public sealed class HeldSaleListItemDto
{
    public Guid Id { get; set; }
    public string HeldNumber { get; set; } = string.Empty;
    public DateTime HeldOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public int? MinutesRemaining { get; set; }
    public string ExpiryText => ExpiresOn is null
        ? "No expiry"
        : MinutesRemaining.GetValueOrDefault() <= 0
            ? "Expired"
            : $"{MinutesRemaining.GetValueOrDefault()} min left";
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public int ItemCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class HeldSaleDto
{
    public Guid Id { get; set; }
    public string HeldNumber { get; set; } = string.Empty;
    public Guid StoreId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Walk-In Customer";
    public DateTime HeldOn { get; set; }
    public DateTime? ExpiresOn { get; set; }
    public decimal InvoiceDiscountAmount { get; set; }
    public string? DiscountReason { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<PosCartLineDto> Lines { get; set; } = [];
}


public sealed class ReceiptReprintRequestDto
{
    [Required]
    public Guid SaleId { get; set; }

    [Required]
    [StringLength(300, MinimumLength = 3)]
    public string Reason { get; set; } = string.Empty;
}

public sealed class ReceiptReprintLogDto
{
    public Guid Id { get; set; }
    public string ReprintedByName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime ReprintedOn { get; set; }
}
