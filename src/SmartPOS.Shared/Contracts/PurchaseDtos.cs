using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class SupplierListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? City { get; set; }
    public string? TaxNumber { get; set; }
    public bool IsActive { get; set; }
    public int PurchaseCount { get; set; }
}

public sealed class SupplierDto
{
    public Guid Id { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(40)]
    public string? Phone { get; set; }

    [EmailAddress, StringLength(200)]
    public string? Email { get; set; }

    [StringLength(300)]
    public string? AddressLine { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(50)]
    public string? TaxNumber { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class SupplierLookupDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string Display => string.IsNullOrWhiteSpace(Phone) ? Name : $"{Name} · {Phone}";
}

public sealed class PurchaseLineInputDto
{
    [Required]
    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    [Range(0.001, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 99999999)]
    public decimal UnitCost { get; set; }
}

public sealed class PurchaseEditDto
{
    public Guid Id { get; set; }

    [Required]
    public Guid StoreId { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IList<PurchaseLineInputDto> Lines { get; set; } = [];
}

public sealed class PurchaseListItemDto
{
    public Guid Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int LineCount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime? ReceivedOn { get; set; }
    public DateTime? OrderedOn { get; set; }
}

public sealed class PurchaseQueryDto
{
    public Guid? StoreId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Status { get; set; }
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class PurchaseDetailDto
{
    public Guid Id { get; set; }
    public string PurchaseNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string StoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ReceivedOn { get; set; }
    public IReadOnlyList<PurchaseLineDetailDto> Lines { get; set; } = [];
}

public sealed class PurchaseLineDetailDto
{
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }
}
