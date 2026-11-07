namespace SmartPOS.Shared.Contracts;

public sealed class ReportQueryDto
{
    public Guid? StoreId { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
}

public sealed class SalesReportSummaryDto
{
    public decimal GrossSales { get; set; }
    public decimal ReturnsTotal { get; set; }
    public decimal NetSales { get; set; }
    public decimal TaxCollected { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
    public int TransactionCount { get; set; }
    public int ReturnCount { get; set; }
    public int VoidCount { get; set; }
    public decimal AverageBasket { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal OtherSales { get; set; }
    public decimal CreditSales { get; set; }
}

public sealed class DailySalesRowDto
{
    public DateTime Date { get; set; }
    public int Transactions { get; set; }
    public int ReturnsCount { get; set; }
    public int VoidCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Returns { get; set; }
    public decimal NetSales { get; set; }
    public decimal Tax { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

public sealed class StoreSalesRowDto
{
    public Guid StoreId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Returns { get; set; }
    public decimal NetSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal AverageBasket { get; set; }
}

public sealed class RegisterSalesRowDto
{
    public Guid RegisterId { get; set; }
    public string RegisterName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public decimal NetSales { get; set; }
    public decimal CashSales { get; set; }
    public decimal CardSales { get; set; }
    public decimal CreditSales { get; set; }
}

public sealed class CashierSalesRowDto
{
    public string CashierUserId { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public int VoidCount { get; set; }
    public int ReturnCount { get; set; }
    public decimal GrossSales { get; set; }
    public decimal Discounts { get; set; }
    public decimal Returns { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageBasket { get; set; }
}

public sealed class HourlySalesRowDto
{
    public int Hour { get; set; }
    public string HourLabel { get; set; } = string.Empty;
    public int Transactions { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageBasket { get; set; }
}

public sealed class PaymentMethodRowDto
{
    public string Method { get; set; } = string.Empty;
    public int PaymentCount { get; set; }
    public decimal Amount { get; set; }
    public decimal PercentOfTotal { get; set; }
}

public sealed class ProductSalesRowDto
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal QuantityReturned { get; set; }
    public decimal NetQuantity { get; set; }
    public decimal Revenue { get; set; }
    public decimal Cost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
    public decimal DiscountAmount { get; set; }
}

public sealed class CategorySalesRowDto
{
    public string CategoryName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

public sealed class BrandSalesRowDto
{
    public string BrandName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal MarginPercent { get; set; }
}

public sealed class DiscountReportRowDto
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal InvoiceDiscount { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal TotalDiscount { get; set; }
    public string? Reason { get; set; }
}

public sealed class VoidSaleReportRowDto
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class ReturnReportRowDto
{
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ProcessedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string ProcessedByName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string RefundMethod { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? Notes { get; set; }
}

public sealed class CurrentStockRowDto
{
    public Guid StoreId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockValue { get; set; }
    public bool IsLowStock { get; set; }
    public bool IsOutOfStock { get; set; }
}

public sealed class StockMovementSummaryRowDto
{
    public string MovementType { get; set; } = string.Empty;
    public int MovementCount { get; set; }
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal NetQuantity { get; set; }
}

public sealed class StockTransferReportRowDto
{
    public string TransferNumber { get; set; } = string.Empty;
    public string SourceStoreName { get; set; } = string.Empty;
    public string DestinationStoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedOn { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }
}

public sealed class TaxReportDto
{
    public decimal TaxableSales { get; set; }
    public decimal GstCollected { get; set; }
    public decimal ExemptSales { get; set; }
    public IReadOnlyList<TaxRateRowDto> ByRate { get; set; } = [];
}

public sealed class TaxRateRowDto
{
    public decimal Rate { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
}

public sealed class PurchaseReportSummaryDto
{
    public int PurchaseCount { get; set; }
    public decimal TotalPurchased { get; set; }
    public decimal TotalTax { get; set; }
    public IReadOnlyList<PurchaseSupplierRowDto> BySupplier { get; set; } = [];
}

public sealed class PurchaseSupplierRowDto
{
    public string SupplierName { get; set; } = string.Empty;
    public int PurchaseCount { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class CustomerReportRowDto
{
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public int InvoiceCount { get; set; }
    public decimal TotalSpend { get; set; }
    public decimal Returns { get; set; }
    public decimal Balance { get; set; }
    public DateTime? LastPurchaseOn { get; set; }
    public decimal AverageBasket { get; set; }
}

public sealed class ShiftCashReportRowDto
{
    public string ShiftNumber { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string OpenedByName { get; set; } = string.Empty;
    public DateTime OpenedOn { get; set; }
    public DateTime? ClosedOn { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal OpeningFloat { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }
}

public sealed class ReportsPageDto
{
    public IReadOnlyList<SalesRegisterRowDto> SalesRegister { get; set; } = [];
    public IReadOnlyList<PurchaseRegisterRowDto> PurchaseRegister { get; set; } = [];
    public IReadOnlyList<StockLedgerReportRowDto> StockLedger { get; set; } = [];
    public IReadOnlyList<SlowMovingStockRowDto> SlowMovingStock { get; set; } = [];
    public IReadOnlyList<WeekdaySalesRowDto> WeekdaySales { get; set; } = [];
    public IReadOnlyList<ProductSalesRowDto> ProductPerformance { get; set; } = [];
    public SalesReportSummaryDto SalesSummary { get; set; } = new();
    public IReadOnlyList<DailySalesRowDto> DailySales { get; set; } = [];
    public IReadOnlyList<StoreSalesRowDto> SalesByStore { get; set; } = [];
    public IReadOnlyList<RegisterSalesRowDto> SalesByRegister { get; set; } = [];
    public IReadOnlyList<CashierSalesRowDto> SalesByCashier { get; set; } = [];
    public IReadOnlyList<HourlySalesRowDto> HourlySales { get; set; } = [];
    public IReadOnlyList<PaymentMethodRowDto> PaymentMix { get; set; } = [];
    public IReadOnlyList<ProductSalesRowDto> TopProducts { get; set; } = [];
    public IReadOnlyList<CategorySalesRowDto> SalesByCategory { get; set; } = [];
    public IReadOnlyList<BrandSalesRowDto> SalesByBrand { get; set; } = [];
    public IReadOnlyList<DiscountReportRowDto> Discounts { get; set; } = [];
    public IReadOnlyList<VoidSaleReportRowDto> Voids { get; set; } = [];
    public IReadOnlyList<ReturnReportRowDto> Returns { get; set; } = [];
    public IReadOnlyList<CurrentStockRowDto> CurrentStock { get; set; } = [];
    public IReadOnlyList<StockMovementSummaryRowDto> StockMovements { get; set; } = [];
    public IReadOnlyList<StockTransferReportRowDto> StockTransfers { get; set; } = [];
    public TaxReportDto Tax { get; set; } = new();
    public PurchaseReportSummaryDto Purchases { get; set; } = new();
    public IReadOnlyList<CustomerReportRowDto> Customers { get; set; } = [];
    public IReadOnlyList<ShiftCashReportRowDto> Shifts { get; set; } = [];
}
