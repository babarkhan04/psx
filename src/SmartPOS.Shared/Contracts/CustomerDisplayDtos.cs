namespace SmartPOS.Shared.Contracts;

public sealed class CustomerDisplayStateDto
{
    public Guid RegisterId { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string RegisterName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = "Walk-In Customer";
    public string Status { get; set; } = "Ready";
    public string? SaleNumber { get; set; }
    public DateTime LastUpdatedOn { get; set; } = DateTime.UtcNow;
    public int ItemCount { get; set; }
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<CustomerDisplayLineDto> Lines { get; set; } = [];
}

public sealed class CustomerDisplayLineDto
{
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
