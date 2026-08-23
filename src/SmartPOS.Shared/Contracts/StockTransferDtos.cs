using System.ComponentModel.DataAnnotations;

namespace SmartPOS.Shared.Contracts;

public sealed class StockTransferQueryDto
{
    public Guid? StoreId { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
    public int Take { get; set; } = 100;
}

public sealed class StockTransferListItemDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public string SourceStoreName { get; set; } = string.Empty;
    public string DestinationStoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedOn { get; set; }
    public string? Notes { get; set; }
    public int LineCount { get; set; }
    public decimal TotalRequested { get; set; }
    public decimal TotalDispatched { get; set; }
    public decimal TotalReceived { get; set; }
}

public sealed class StockTransferDetailDto
{
    public Guid Id { get; set; }
    public string TransferNumber { get; set; } = string.Empty;
    public Guid SourceStoreId { get; set; }
    public string SourceStoreName { get; set; } = string.Empty;
    public Guid DestinationStoreId { get; set; }
    public string DestinationStoreName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime RequestedOn { get; set; }
    public DateTime? ApprovedOn { get; set; }
    public DateTime? DispatchedOn { get; set; }
    public DateTime? ReceivedOn { get; set; }
    public string? Notes { get; set; }
    public IList<StockTransferLineDto> Lines { get; set; } = [];
}

public sealed class StockTransferLineDto
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? VariantName { get; set; }
    public decimal QuantityRequested { get; set; }
    public decimal QuantityDispatched { get; set; }
    public decimal QuantityReceived { get; set; }
}

public sealed class CreateStockTransferDto
{
    [Required]
    public Guid SourceStoreId { get; set; }

    [Required]
    public Guid DestinationStoreId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IList<CreateStockTransferLineDto> Lines { get; set; } = [];
}

public sealed class CreateStockTransferLineDto
{
    [Required]
    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    [Range(typeof(decimal), "0.001", "999999999")]
    public decimal Quantity { get; set; }
}

public sealed class ReceiveStockTransferDto
{
    [Required]
    public Guid TransferId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public IList<ReceiveStockTransferLineDto> Lines { get; set; } = [];
}

public sealed class ReceiveStockTransferLineDto
{
    [Required]
    public Guid LineId { get; set; }

    [Range(typeof(decimal), "0", "999999999")]
    public decimal QuantityReceived { get; set; }
}

public sealed class StockTransferActionDto
{
    [Required]
    public Guid TransferId { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }
}
