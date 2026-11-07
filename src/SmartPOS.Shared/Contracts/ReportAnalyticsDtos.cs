namespace SmartPOS.Shared.Contracts;

public sealed class SalesRegisterRowDto
{
    public string SaleNumber { get; set; } = string.Empty;
    public DateTime CompletedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string CashierName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string PaymentMethods { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal Discounts { get; set; }
    public decimal Tax { get; set; }
    public decimal RefundsInPeriod { get; set; }
    public decimal NetSales { get; set; }
}

public sealed class PurchaseRegisterRowDto
{
    public string PurchaseNumber { get; set; } = string.Empty;
    public DateTime ReceivedOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal Quantity { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class StockLedgerReportRowDto
{
    public DateTime OccurredOn { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MovementType { get; set; } = string.Empty;
    public decimal QuantityIn { get; set; }
    public decimal QuantityOut { get; set; }
    public decimal QuantityAfter { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public sealed class SlowMovingStockRowDto
{
    public string StoreName { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal QuantityOnHand { get; set; }
    public decimal QuantitySold { get; set; }
    public decimal StockValue { get; set; }
    public decimal StockCoverDays { get; set; }
    public DateTime? LastSaleInPeriod { get; set; }
}

public sealed class WeekdaySalesRowDto
{
    public int DayOrder { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int DaysInPeriod { get; set; }
    public int Transactions { get; set; }
    public decimal NetSales { get; set; }
    public decimal AverageDailySales { get; set; }
}
