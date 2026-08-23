using SmartPOS.Domain.Common;
using SmartPOS.Domain.Enums;

namespace SmartPOS.Domain.Entities;

/// <summary>Immutable ledger entry — every stock change must create one of these.</summary>
public class StockLedger : StoreScopedEntity
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public StockMovementType MovementType { get; set; }
    public decimal QuantityChange { get; set; }
    public decimal QuantityAfter { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }

    public Product? Product { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    public Store? Store { get; set; }
}
